from PIL import ImageFont
P='/workspace/work/fonts/NotoSansSC.ttf'
def F(size, w=400):
    f=ImageFont.truetype(P,size)
    try: f.set_variation_by_axes([w])
    except Exception: pass
    return f
