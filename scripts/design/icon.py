import math
from PIL import Image, ImageDraw, ImageFilter
INK=(31,36,33); INK2=(42,49,45); PAPER=(244,245,242); SEAL=(200,69,47)
PTS=[(0.00,0.045),(0.05,0.035),(0.30,0.05),(0.62,0.035),(0.80,0.01),(0.875,-0.045),(0.94,-0.02),
     (0.995,0.08),(0.985,0.15),(0.93,0.20),(0.86,0.19),(0.78,0.16),(0.55,0.155),(0.25,0.165),(0.08,0.185),(0.03,0.16)]
BASE=list(PTS)
def catmull(pts,steps=24):
    out=[];n=len(pts)
    for i in range(n):
        p0,p1,p2,p3=pts[i-1],pts[i],pts[(i+1)%n],pts[(i+2)%n]
        for k in range(steps):
            t=k/steps;t2=t*t;t3=t2*t
            out.append(tuple(0.5*((2*p1[j])+(-p0[j]+p2[j])*t+(2*p0[j]-5*p1[j]+4*p2[j]-p3[j])*t2+(-p0[j]+3*p1[j]-3*p2[j]+p3[j])*t3) for j in range(2)))
    return out
def stroke(dr,S,box,col):
    x0,y0,x1,y1=box; W=x1-x0
    pts=[(x0+px*W, y0+py*W) for px,py in catmull(PTS)]
    dr.polygon(pts,fill=col)
def app_icon(size, seal=True):
    S=4; N=size*S
    im=Image.new('RGBA',(N,N),(0,0,0,0)); dr=ImageDraw.Draw(im)
    pad=N*0.04 if size>=48 else 0
    rad=N*0.23
    # vertical ink gradient tile
    tile=Image.new('RGBA',(N,N)); td=ImageDraw.Draw(tile)
    for y in range(N):
        t=y/N; c=tuple(round(INK2[k]*(1-t)+INK[k]*t) for k in range(3)); td.line((0,y,N,y),fill=c+(255,))
    mask=Image.new('L',(N,N),0); ImageDraw.Draw(mask).rounded_rectangle((pad,pad,N-pad,N-pad),rad,fill=255)
    im.paste(tile,(0,0),mask)
    dr=ImageDraw.Draw(im)
    w=N*(0.62 if size>=32 else 0.70); sc=1.4 if size>=32 else 1.9
    PTS[:]=[(x,y*sc) for x,y in BASE]
    stroke(dr,S,((N-w)/2,N*0.45-w*0.09*sc,(N+w)/2,0),PAPER+(255,))
    if seal and size>=32:
        s=N*0.10; x=N*0.68; y=N*0.68
        dr.rounded_rectangle((x,y,x+s,y+s),s*0.18,fill=SEAL+(255,))
    return im.resize((size,size),Image.LANCZOS)
if __name__=='__main__':
    import sys
    out=sys.argv[1]
    big=app_icon(256); big.save(out+'/yiwei-256.png')
    sizes=[16,20,24,32,40,48,64,256]
    imgs=[app_icon(s) for s in sizes]
    imgs[-1].save(out+'/yiwei.ico',format='ICO',sizes=[(s,s) for s in sizes],append_images=imgs[:-1])
    prev=Image.new('RGBA',(560,300),(238,241,239,255))
    prev.alpha_composite(big,(10,20)); x=290
    for s in [64,48,32,24,16]:
        prev.alpha_composite(app_icon(s),(x,40)); x+=s+12
    dk=Image.new('RGBA',(270,120),(20,22,21,255)); x=10
    for s in [64,32,16]:
        dk.alpha_composite(app_icon(s),(x,20)); x+=s+16
    prev.alpha_composite(dk,(285,160)); prev.save('/tmp/v/icon_prev.png')
