import sys; sys.path.insert(0,'/workspace/work/yiwei-ime/scripts'); sys.path.insert(0,'/workspace/work/yiwei-ime/scripts/design')
from PIL import Image, ImageDraw, ImageFilter
from fonts import F
import icon
from brand_schemes import BRANDS, scheme
from skins import card, rgba
PAPER=(244,245,242); INK=(31,36,33); MUTE=(106,115,110); SEAL=(200,69,47)
S=2; W,H=1280*S,640*S
im=Image.new('RGBA',(W,H),PAPER+(255,)); d=ImageDraw.Draw(im)
icon.PTS[:]=[(x,y*0.55) for x,y in icon.BASE]
icon.stroke(d,S,(110*S,190*S,560*S,0),INK+(255,))
s=26*S; d.rounded_rectangle((522*S,258*S,522*S+s,258*S+s),6*S,fill=SEAL+(255,))
d.text((110*S,350*S),"一维输入法",font=F(64*S,600),fill=INK+(255,))
d.text((112*S,440*S),"中文常新，自在表达",font=F(26*S),fill=MUTE+(255,))
d.text((112*S,486*S),"开源免费的 Windows 输入法，基于 RIME，输入只在本机处理",font=F(19*S),fill=MUTE+(255,))
# one candidate window, big, the real thing
c=card(scheme('qingbi',0x0E8C7A,False)); c=c.resize((int(c.width*1.45),int(c.height*1.45)),Image.LANCZOS)
im.alpha_composite(c,(690*S,210*S))
cd=card(scheme('qingbi',0x0E8C7A,True)); cd=cd.resize((int(cd.width*1.45),int(cd.height*1.45)),Image.LANCZOS)
im.alpha_composite(cd,(690*S,370*S))
x=720*S
for bid,name,rgb in BRANDS:
    d.ellipse((x,560*S,x+16*S,576*S),fill=((rgb>>16)&255,(rgb>>8)&255,rgb&255,255)); d.text((x+24*S,556*S),name,font=F(15*S),fill=MUTE+(255,)); x+=92*S
im.resize((1280,640),Image.LANCZOS).convert('RGB').save(sys.argv[1])
