#pragma once

// 一维输入法：「界面风格设定」对话框（替换 Weasel 原版，见 scripts/rebrand.py）
// 一维配色排最前、原版配色隐藏（正在用的除外），预览按配色实时绘制「yi wei / 一维输入法」，
// 可设字体；装了一维助手时，确定后交给助手保存（与 设置 → 外观 同一份设置）。

#include "resource.h"
#include "UIStyleSettings.h"

class UIStyleSettingsDialog : public CDialogImpl<UIStyleSettingsDialog> {
 public:
  enum { IDD = IDD_STYLE_SETTING };

  UIStyleSettingsDialog(UIStyleSettings* settings);
  ~UIStyleSettingsDialog();

 protected:
  BEGIN_MSG_MAP(UIStyleSettingsDialog)
  MESSAGE_HANDLER(WM_INITDIALOG, OnInitDialog)
  MESSAGE_HANDLER(WM_CLOSE, OnClose)
  COMMAND_ID_HANDLER(IDOK, OnOK)
  COMMAND_ID_HANDLER(IDC_SELECT_FONT, OnSelectFont)
  COMMAND_HANDLER(IDC_COLOR_SCHEME, LBN_SELCHANGE, OnColorSchemeSelChange)
  END_MSG_MAP()

  LRESULT OnInitDialog(UINT, WPARAM, LPARAM, BOOL&);
  LRESULT OnClose(UINT, WPARAM, LPARAM, BOOL&);
  LRESULT OnOK(WORD, WORD, HWND, BOOL&);
  LRESULT OnSelectFont(WORD, WORD, HWND, BOOL&);
  LRESULT OnColorSchemeSelChange(WORD, WORD, HWND, BOOL&);

  void Populate();
  void Preview(int index);
  void LoadFont();

  UIStyleSettings* settings_;
  bool loaded_;
  std::vector<ColorSchemeInfo> preset_;
  std::string active_;
  std::string selected_;
  std::wstring font_face_;
  int font_point_;
  bool font_changed_;

  CListBox color_schemes_;
  CStatic preview_;
  CImage image_;
  CButton select_font_;
};
