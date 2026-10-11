#include "stdafx.h"
#include "UIStyleSettingsDialog.h"
#include "UIStyleSettings.h"
#include "Configurator.h"
#include <WeaselUtility.h>
#include <commdlg.h>
#include <algorithm>
#include <cctype>
#include <cstdlib>
#include <cwchar>
#include <string>
#include <vector>

#pragma comment(lib, "comdlg32.lib")

// 一维输入法：替换 Weasel 原版「界面风格设定」（由 scripts/rebrand.py 拷入）。

namespace {

inline int YwMin(int a, int b) { return a < b ? a : b; }
inline int YwMax(int a, int b) { return a > b ? a : b; }

// 一维自己的配色，按这个顺序排在最前（与一维助手 设置 → 外观 一致）
const char* const kYiweiOrder[] = {
    "yiwei_pill",   "yiwei_pill_dark",   "yiwei_moblue", "yiwei_moblue_dark",
    "yiwei_qingbi", "yiwei_qingbi_dark", "yiwei_zhusha", "yiwei_zhusha_dark",
    "yiwei_dianzi", "yiwei_dianzi_dark", "yiwei_shimo",  "yiwei_shimo_dark",
    "yiwei_light",  "yiwei_dark",        "graphite",     "midnight",
    "sakura",       "matcha",            "nord",         "paper",
    "yiwei_custom"};

// Weasel 自带的配色（繁体名）：默认不列出，正在使用的那一个除外
const char* const kUpstream[] = {/*UPSTREAM_IDS*/};

const wchar_t* const kCandidates[] = {L"\u4e00\u7ef4\u8f93\u5165\u6cd5",
                                      L"\u4e00\u7ef4", L"\u4f9d\u504e",
                                      L"\u4e00\u4f4d", L"\u533b\u536b"};

int Rank(const std::string& id) {
  for (size_t i = 0; i < _countof(kYiweiOrder); ++i)
    if (id == kYiweiOrder[i])
      return (int)i;
  if (id.compare(0, 6, "yiwei_") == 0)
    return 100;
  return 1000;
}

bool IsUpstream(const std::string& id) {
  for (size_t i = 0; i < _countof(kUpstream); ++i)
    if (id == kUpstream[i])
      return true;
  return false;
}

std::wstring HelperPath() {
  WCHAR path[MAX_PATH] = {0};
  GetModuleFileNameW(NULL, path, _countof(path));
  std::wstring p(path);
  size_t slash = p.find_last_of(L"\\/");
  if (slash == std::wstring::npos)
    return std::wstring();
  return p.substr(0, slash + 1) + L"YiweiHelper.exe";
}

bool ParseHex(std::string t, unsigned* out) {
  if (t.empty())
    return false;
  if (t[0] == '#')
    t = t.substr(1);
  else if (t.size() > 2 && t[0] == '0' && (t[1] == 'x' || t[1] == 'X'))
    t = t.substr(2);
  if (t.empty() || t.size() > 8)
    return false;
  for (char c : t)
    if (!isxdigit((unsigned char)c))
      return false;
  *out = (unsigned)strtoul(t.c_str(), nullptr, 16);
  return true;
}

// 读一种颜色；format 为 argb / rgba / abgr（Weasel 默认 abgr）
bool GetColor(RimeConfig* config,
              const std::string& base,
              const char* key,
              const std::string& format,
              COLORREF* out) {
  RimeApi* rime = rime_get_api();
  std::string path = base + "/" + key;
  unsigned u = 0;
  const char* s = rime->config_get_cstring(config, path.c_str());
  if (s && ParseHex(s, &u)) {
    // ok
  } else {
    int v = 0;
    if (!rime->config_get_int(config, path.c_str(), &v))
      return false;
    u = (unsigned)v;
  }
  unsigned r, g, b;
  if (format == "argb") {
    r = (u >> 16) & 0xFF; g = (u >> 8) & 0xFF; b = u & 0xFF;
  } else if (format == "rgba") {
    r = (u >> 24) & 0xFF; g = (u >> 16) & 0xFF; b = (u >> 8) & 0xFF;
  } else {
    r = u & 0xFF; g = (u >> 8) & 0xFF; b = (u >> 16) & 0xFF;
  }
  *out = RGB(r, g, b);
  return true;
}

void FillRound(HDC dc, const RECT& rc, int radius, COLORREF fill, COLORREF line) {
  HBRUSH brush = CreateSolidBrush(fill);
  HPEN pen = CreatePen(PS_SOLID, 1, line);
  HGDIOBJ ob = SelectObject(dc, brush);
  HGDIOBJ op = SelectObject(dc, pen);
  RoundRect(dc, rc.left, rc.top, rc.right, rc.bottom, radius * 2, radius * 2);
  SelectObject(dc, ob);
  SelectObject(dc, op);
  DeleteObject(brush);
  DeleteObject(pen);
}

}  // namespace

// 打开一维助手（与部署程序在同一文件夹）；找不到助手时返回 false
bool YiweiHelperRun(const wchar_t* args) {
  std::wstring exe = HelperPath();
  if (exe.empty() || GetFileAttributesW(exe.c_str()) == INVALID_FILE_ATTRIBUTES)
    return false;
  AllowSetForegroundWindow(ASFW_ANY);
  HINSTANCE h = ShellExecuteW(NULL, L"open", exe.c_str(), args, NULL,
                              SW_SHOWNORMAL);
  return (INT_PTR)h > 32;
}

UIStyleSettingsDialog::UIStyleSettingsDialog(UIStyleSettings* settings)
    : settings_(settings), loaded_(false), font_point_(0), font_changed_(false) {}

UIStyleSettingsDialog::~UIStyleSettingsDialog() {
  image_.Destroy();
}

void UIStyleSettingsDialog::LoadFont() {
  font_face_ = L"Microsoft YaHei UI";
  font_point_ = 12;
  RimeConfig config = {0};
  if (!settings_ || !settings_->GetConfig(&config))
    return;
  RimeApi* rime = rime_get_api();
  const char* face = rime->config_get_cstring(&config, "style/font_face");
  if (face && *face) {
    std::wstring f = u8tow(face);
    size_t comma = f.find(L',');
    if (comma != std::wstring::npos)
      f = f.substr(0, comma);
    size_t a = f.find_first_not_of(L" \t\"");
    size_t b = f.find_last_not_of(L" \t\"");
    if (a != std::wstring::npos)
      font_face_ = f.substr(a, b - a + 1);
  }
  int pt = 0;
  if (rime->config_get_int(&config, "style/font_point", &pt) && pt >= 6 &&
      pt <= 72)
    font_point_ = pt;
}

void UIStyleSettingsDialog::Populate() {
  if (!settings_)
    return;
  active_ = settings_->GetActiveColorScheme();
  std::vector<ColorSchemeInfo> all;
  settings_->GetPresetColorSchemes(&all);
  preset_.clear();
  for (const auto& info : all) {
    if (info.color_scheme_id == "yiwei_auto_dark")
      continue;  // 一维助手按浅色配色自动生成的深色版
    if (IsUpstream(info.color_scheme_id) && info.color_scheme_id != active_)
      continue;
    preset_.push_back(info);
  }
  std::stable_sort(preset_.begin(), preset_.end(),
                   [](const ColorSchemeInfo& a, const ColorSchemeInfo& b) {
                     return Rank(a.color_scheme_id) < Rank(b.color_scheme_id);
                   });
  int active_index = -1;
  for (size_t i = 0; i < preset_.size(); ++i) {
    std::wstring txt = u8tow(preset_[i].name);
    color_schemes_.AddString(txt.c_str());
    if (preset_[i].color_scheme_id == active_)
      active_index = (int)i;
  }
  if (active_index < 0 && !preset_.empty())
    active_index = 0;
  if (active_index >= 0) {
    color_schemes_.SetCurSel(active_index);
    selected_ = preset_[active_index].color_scheme_id;
    Preview(active_index);
  }
  loaded_ = true;
}

LRESULT UIStyleSettingsDialog::OnInitDialog(UINT, WPARAM, LPARAM, BOOL&) {
  color_schemes_.Attach(GetDlgItem(IDC_COLOR_SCHEME));
  preview_.Attach(GetDlgItem(IDC_PREVIEW));
  select_font_.Attach(GetDlgItem(IDC_SELECT_FONT));
  select_font_.EnableWindow(TRUE);

  LoadFont();
  Populate();

  CenterWindow();
  BringWindowToTop();
  return TRUE;
}

LRESULT UIStyleSettingsDialog::OnClose(UINT, WPARAM, LPARAM, BOOL&) {
  EndDialog(IDCANCEL);
  return 0;
}

LRESULT UIStyleSettingsDialog::OnOK(WORD, WORD, HWND, BOOL&) {
  // 装了一维助手：交给助手保存并部署（写进同一份设置，设置 → 外观 会显示同样的选择）
  bool scheme_changed = !selected_.empty() && selected_ != active_;
  if (scheme_changed || font_changed_) {
    std::wstring args = L"/style ";
    if (scheme_changed)
      args += u8tow(selected_);
    args += L"|";
    if (font_changed_)
      args += std::to_wstring(font_point_) + L"|" + font_face_;
    if (YiweiHelperRun(args.c_str())) {
      EndDialog(IDCANCEL);  // 不再由部署程序另写 weasel.custom.yaml
      return 0;
    }
  } else if (GetFileAttributesW(HelperPath().c_str()) !=
             INVALID_FILE_ATTRIBUTES) {
    EndDialog(IDCANCEL);  // 没有改动
    return 0;
  }
  EndDialog(IDOK);
  return 0;
}

LRESULT UIStyleSettingsDialog::OnSelectFont(WORD, WORD, HWND, BOOL&) {
  LOGFONTW lf = {0};
  HDC screen = ::GetDC(NULL);
  int dpi = GetDeviceCaps(screen, LOGPIXELSY);
  ::ReleaseDC(NULL, screen);
  lf.lfHeight = -MulDiv(font_point_ > 0 ? font_point_ : 12, dpi, 72);
  lf.lfCharSet = DEFAULT_CHARSET;
  wcsncpy_s(lf.lfFaceName, font_face_.c_str(), _TRUNCATE);
  CHOOSEFONTW cf = {0};
  cf.lStructSize = sizeof(cf);
  cf.hwndOwner = m_hWnd;
  cf.lpLogFont = &lf;
  cf.Flags = CF_SCREENFONTS | CF_INITTOLOGFONTSTRUCT | CF_NOVERTFONTS |
             CF_LIMITSIZE;
  cf.nSizeMin = 8;
  cf.nSizeMax = 48;
  if (!ChooseFontW(&cf))
    return 0;
  font_face_ = lf.lfFaceName;
  font_point_ = cf.iPointSize / 10;
  if (font_point_ < 8)
    font_point_ = 8;
  font_changed_ = true;
  std::string face8 = wtou8(font_face_);
  settings_->CustomizeString("style/font_face", face8.c_str());
  settings_->CustomizeInt("style/font_point", font_point_);
  Preview(color_schemes_.GetCurSel());
  return 0;
}

LRESULT UIStyleSettingsDialog::OnColorSchemeSelChange(WORD, WORD, HWND, BOOL&) {
  int index = color_schemes_.GetCurSel();
  if (index >= 0 && index < (int)preset_.size()) {
    selected_ = preset_[index].color_scheme_id;
    settings_->SelectColorScheme(selected_);
    Preview(index);
  }
  return 0;
}

// 按配色画一个候选框：编码「yi wei」+ 候选「一维输入法 一维 依偎 一位 医卫」
void UIStyleSettingsDialog::Preview(int index) {
  if (index < 0 || index >= (int)preset_.size())
    return;
  RECT client;
  preview_.GetClientRect(&client);
  int w = client.right - client.left, h = client.bottom - client.top;
  if (w <= 0 || h <= 0)
    return;
  RimeConfig config = {0};
  if (!settings_->GetConfig(&config))
    return;
  const std::string base = "preset_color_schemes/" + preset_[index].color_scheme_id;
  const char* fmt = rime_get_api()->config_get_cstring(
      &config, (base + "/color_format").c_str());
  std::string format = fmt ? fmt : "abgr";
  for (auto& c : format)
    c = (char)tolower((unsigned char)c);

  COLORREF back = RGB(255, 255, 255), border = RGB(225, 229, 238),
           text = RGB(28, 32, 40);
  GetColor(&config, base, "back_color", format, &back);
  GetColor(&config, base, "text_color", format, &text);
  if (!GetColor(&config, base, "border_color", format, &border))
    border = back;
  COLORREF cand = text;
  GetColor(&config, base, "candidate_text_color", format, &cand);
  COLORREF hl_back = back;
  GetColor(&config, base, "hilited_back_color", format, &hl_back);
  COLORREF hl_text = text;
  GetColor(&config, base, "hilited_text_color", format, &hl_text);
  COLORREF hc_back = hl_back;
  GetColor(&config, base, "hilited_candidate_back_color", format, &hc_back);
  COLORREF hc_text = hl_text;
  GetColor(&config, base, "hilited_candidate_text_color", format, &hc_text);
  COLORREF label = cand;
  GetColor(&config, base, "label_color", format, &label);
  COLORREF hc_label = hc_text;
  if (!GetColor(&config, base, "hilited_label_color", format, &hc_label))
    GetColor(&config, base, "hilited_candidate_label_color", format, &hc_label);

  int corner = 6, round = 4;
  RimeApi* rime = rime_get_api();
  rime->config_get_int(&config, "style/layout/corner_radius", &corner);
  rime->config_get_int(&config, "style/layout/round_corner", &round);

  CImage next;
  if (!next.Create(w, h, 24))
    return;
  HDC dc = next.GetDC();
  RECT all = {0, 0, w, h};
  HBRUSH face = CreateSolidBrush(GetSysColor(COLOR_3DFACE));
  FillRect(dc, &all, face);
  DeleteObject(face);

  int dpi = GetDeviceCaps(dc, LOGPIXELSY);
  int pt = YwMin(YwMax(font_point_, 9), 15);
  HFONT font = CreateFontW(-MulDiv(pt, dpi, 72), 0, 0, 0, FW_NORMAL, FALSE,
                           FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS,
                           CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
                           DEFAULT_PITCH, font_face_.c_str());
  HGDIOBJ old_font = SelectObject(dc, font);
  SetBkMode(dc, TRANSPARENT);
  TEXTMETRICW tm = {0};
  GetTextMetricsW(dc, &tm);
  int line = tm.tmHeight + MulDiv(8, dpi, 96);
  int pad = MulDiv(8, dpi, 96);

  int box_h = YwMin(h - 2 * pad, line * 6 + pad * 2);
  RECT box = {pad, (h - box_h) / 2, w - pad, (h - box_h) / 2 + box_h};
  FillRound(dc, box, YwMin(corner, 16), back, border);

  // 编码行
  const wchar_t* preedit = L"yi wei";
  SIZE sz = {0};
  GetTextExtentPoint32W(dc, preedit, (int)wcslen(preedit), &sz);
  RECT pe = {box.left + pad, box.top + pad, box.left + pad * 2 + sz.cx,
             box.top + pad + line - MulDiv(4, dpi, 96)};
  if (hl_back != back)
    FillRound(dc, pe, YwMin(round, (int)((pe.bottom - pe.top) / 2)), hl_back,
              hl_back);
  SetTextColor(dc, hl_text);
  RECT pt_rc = pe;
  DrawTextW(dc, preedit, -1, &pt_rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

  // 候选
  for (int i = 0; i < 5; ++i) {
    int top = box.top + pad + line * (i + 1);
    if (top + line > box.bottom)
      break;
    RECT row = {box.left + pad / 2, top, box.right - pad / 2,
                top + line - MulDiv(4, dpi, 96)};
    if (i == 0)
      FillRound(dc, row, YwMin(round, (int)((row.bottom - row.top) / 2)),
                hc_back, hc_back);
    wchar_t num[8];
    swprintf_s(num, L"%d.", i + 1);
    RECT lr = {row.left + pad, row.top, row.left + pad + tm.tmAveCharWidth * 3,
               row.bottom};
    SetTextColor(dc, i == 0 ? hc_label : label);
    DrawTextW(dc, num, -1, &lr, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
    RECT tr = {lr.right, row.top, row.right - pad, row.bottom};
    SetTextColor(dc, i == 0 ? hc_text : cand);
    DrawTextW(dc, kCandidates[i], -1, &tr,
              DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
  }

  SelectObject(dc, old_font);
  DeleteObject(font);
  next.ReleaseDC();

  preview_.SetBitmap((HBITMAP)next);
  image_.Destroy();
  image_.Attach(next.Detach());
}
