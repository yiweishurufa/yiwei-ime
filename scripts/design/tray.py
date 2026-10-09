import sys; sys.path.insert(0,'/workspace/work/design')
from PIL import Image, ImageDraw
from fonts import F
INK=(31,36,33); PAPER=(244,245,242); JADE=(14,140,122); MIST=(107,115,110)
def tile(size, ch, bg, bar):
    S=8;N=size*S; im=Image.new('RGBA',(N,N),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.rounded_rectangle((0,0,N-1,N-1),N*0.24,fill=bg+(255,))
    f=F(int(N*0.60),700); bb=d.textbbox((0,0),ch,font=f); w=bb[2]-bb[0]; h=bb[3]-bb[1]
    d.text(((N-w)/2-bb[0],(N*0.46-h/2)-bb[1]),ch,font=f,fill=PAPER+(255,))
    if bar: d.rounded_rectangle((N*0.26,N*0.80,N*0.74,N*0.87),N*0.035,fill=bar+(255,))
    return im.resize((size,size),Image.LANCZOS)
sizes=[16,20,24,32,48,64]
for name,ch,bg,bar in [('zh','中',INK,JADE),('en','英',MIST,None)]:
    ims=[tile(s,ch,bg,bar) for s in sizes]
    ims[-1].save('out/%s.ico'%name,format='ICO',sizes=[(s,s) for s in sizes],append_images=ims[:-1])
p=Image.new('RGBA',(200,80),(238,241,239,255)); x=8
for s in [64,32,16]: p.alpha_composite(tile(s,'中',INK,JADE),(x,8)); x+=s+8
p.alpha_composite(tile(32,'英',MIST,None),(x,8)); p.save('/tmp/v/tray.png')
