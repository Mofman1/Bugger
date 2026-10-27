"""Blue-shell variation of the original layered beetle icon."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'outputs' / 'icons'
SOURCE = ROOT / 'design' / 'icons'
base = (SOURCE / 'little-beetle.svg').read_text(encoding='utf-8')
base = base.replace('fill="#d8a442"', 'fill="#6099c3"').replace('stroke="#ffe4a7"', 'stroke="#bddcf2"')
for slug, content in [('blue-beetle', base), ('blue-beetle-alert', base.replace('</svg>', '<circle cx="49" cy="16" r="10" fill="#e68a22" stroke="#fff" stroke-width="2.8"/>\n</svg>'))]:
    ET.fromstring(content)
    (SOURCE / f'{slug}.svg').write_text(content, encoding='utf-8')
    (OUT / f'{slug}.svg').write_text(content, encoding='utf-8')
subprocess.run(['node', str(ROOT / 'design' / 'render-icons.cjs'), str(OUT)], check=True)
for slug in ('blue-beetle', 'blue-beetle-alert'):
    with Image.open(OUT / f'{slug}-256.png') as image:
        image.save(OUT / f'{slug}.ico', sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])

def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'), size)

sheet = Image.new('RGB',(720,430),'#eeeeee')
d = ImageDraw.Draw(sheet)
d.text((22,16),'Blue beetle with reminder status',font=font(24,True),fill='#222')
for i,(slug,title) in enumerate([('blue-beetle','No active reminder'),('blue-beetle-alert','Unsnoozed reminder')]):
    x = 18+i*350
    d.rounded_rectangle((x,65,x+334,410),radius=3,fill='#fafafa',outline='#b7b7b7')
    d.text((x+16,80),title,font=font(18,True),fill='#222')
    with Image.open(OUT / f'{slug}-128.png') as im:
        sheet.paste(im,(x+103,112),im)
    for y,bg,fg,label in [(260,'#e8e8e8','#444','Light tray'),(326,'#292c31','#eee','Dark tray')]:
        d.rectangle((x+12,y,x+322,y+49),fill=bg)
        d.text((x+20,y+14),label,font=font(13),fill=fg)
        for j,size in enumerate((16,24,32)):
            px=x+131+j*66
            with Image.open(OUT / f'{slug}-{size}.png') as im:
                sheet.paste(im,(px,y+(49-size)//2),im)
    for j,label in enumerate(('16','24','32 px')):
        d.text((x+133+j*66,386),label,font=font(11),fill='#555')
sheet.save(OUT / 'blue-beetle-preview.png')
print('Saved '+str(OUT / 'blue-beetle-preview.png'))

