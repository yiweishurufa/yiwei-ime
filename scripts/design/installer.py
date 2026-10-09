import sys; sys.path.insert(0,'/workspace/work/design')
from PIL import Image, ImageDraw
from fonts import F
import icon
PAPER=(244,245,242); INK=(31,36,33); MUTE=(106,115,110); SEAL=(200,69,47); LINE=(221,227,223)
def welcome():
    S=3; W,H=164*S,314*S
    im=Image.new('RGBA',(W,H),PAPER+(255,)); d=ImageDraw.Draw(im)
    # the single brushed stroke, large, ink on paper
    icon.PTS[:]=[(x,y*0.75) for x,y in icon.BASE]
    w=W*0.78; icon.stroke(d,S,((W-w)/2,H*0.40,(W+w)/2,0),INK+(255,))
    s=W*0.075; d.rounded_rectangle((W*0.74,H*0.50,W*0.74+s,H*0.50+s),s*0.18,fill=SEAL+(255,))
    ft=F(22*S,600); fs=F(10*S,400)
    d.text((W*0.12,H*0.66),"一维输入法",font=ft,fill=INK+(255,))
    d.text((W*0.12,H*0.66+34*S),"中文常新，自在表达",font=fs,fill=MUTE+(255,))
    d.line((W*0.12,H*0.90,W*0.88,H*0.90),fill=LINE,width=S)
    d.text((W*0.12,H*0.90+6*S),"开源免费，输入只在本机处理",font=F(8*S),fill=MUTE+(255,))
    return im.resize((164,314),Image.LANCZOS).convert('RGB')
def header():
    S=4; W,H=150*S,57*S
    im=Image.new('RGBA',(W,H),(255,255,255,255))
    ic=icon.app_icon(40*S//S*S if False else 160)  # 160px then shrink
    ic=ic.resize((40*S,40*S),Image.LANCZOS)
    im.alpha_composite(ic,(W-48*S,int(8.5*S)))
    return im.resize((150,57),Image.LANCZOS).convert('RGB')
w=welcome(); w.save('out/installer-welcome.bmp'); w.save('/tmp/v/iw.png')
h=header(); h.save('out/installer-header.bmp'); h.resize((300,114)).save('/tmp/v/ih.png')
