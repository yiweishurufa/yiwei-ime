import sys; sys.path.insert(0,'/workspace/work/yiwei-ime/scripts'); sys.path.insert(0,'/workspace/work/design')
from PIL import Image, ImageDraw, ImageFilter
from brand_schemes import BRANDS, scheme
from fonts import F
S=2
def rgba(v): return ((v>>16)&255,(v>>8)&255,v&255,(v>>24)&255)
def card(d):
    fz=F(16*S); fl=F(13*S); fp=F(14*S)
    w,h=330*S,74*S
    im=Image.new('RGBA',(w+40*S,h+40*S),(0,0,0,0))
    sh=Image.new('RGBA',im.size,(0,0,0,0)); ImageDraw.Draw(sh).rounded_rectangle((20*S,22*S,20*S+w,22*S+h),10*S,fill=rgba(d['shadow_color']))
    im=Image.alpha_composite(im,sh.filter(ImageFilter.GaussianBlur(6*S)))
    dr=ImageDraw.Draw(im); x0,y0=20*S,20*S
    dr.rounded_rectangle((x0,y0,x0+w,y0+h),10*S,fill=rgba(d['back_color']),outline=rgba(d['border_color']),width=S)
    dr.text((x0+12*S,y0+8*S),"yi'wei",font=fp,fill=rgba(d['text_color']))
    x=x0+8*S; y=y0+36*S
    for i,c in enumerate(['一维','依偎','意味','以为','一位']):
        lw=dr.textlength(f'{i+1}',font=fl); cw=dr.textlength(c,font=fz); bw=lw+cw+16*S
        if i==0:
            dr.rounded_rectangle((x,y-3*S,x+bw,y+25*S),6*S,fill=rgba(d['hilited_candidate_back_color']))
        dr.text((x+6*S,y+3*S),f'{i+1}',font=fl,fill=rgba(d['hilited_label_color' if i==0 else 'label_color']))
        dr.text((x+10*S+lw,y-1*S),c,font=fz,fill=rgba(d['hilited_candidate_text_color' if i==0 else 'candidate_text_color']))
        x+=bw+8*S
    return im
def sheet(path):
    cols=len(BRANDS); cw,ch=370*S,114*S
    out=Image.new('RGBA',(cw*cols//1, ch*2+20*S),(0,0,0,0))
    for j,dark in enumerate([False,True]):
        bg=Image.new('RGBA',(cw*cols,ch+10*S),(238,241,239,255) if not dark else (14,16,15,255))
        out.paste(bg,(0,j*(ch+10*S)))
        for i,(bid,name,rgb) in enumerate(BRANDS):
            c=card(scheme(bid,rgb,dark)); out.alpha_composite(c,(i*cw,j*(ch+10*S)))
    out.save(path)
if __name__=='__main__': sheet(sys.argv[1] if len(sys.argv)>1 else '/tmp/v/skins.png')
