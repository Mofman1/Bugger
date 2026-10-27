"""Layer an alert badge on the existing original beetle vector and render comparisons."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'outputs' / 'icons'
base = (ROOT / 'design' / 'icons' / 'little-beetle.svg').read_text(encoding='utf-8')
alert = base.replace('</svg>', '<circle cx="49" cy="16" r="10" fill="#ce3936" stroke="#fff" stroke-width="2.8"/>\n</svg>')
ET.fromstring(alert)
(ROOT / 'design' / 'icons' / 'little-beetle-alert.svg').write_text(alert, encoding='utf-8')
(OUT / 'little-beetle-alert.svg').write_text(alert, encoding='utf-8')
subprocess.run(['node', str(ROOT / 'design' / 'render-icons.cjs'), str(OUT)], check=True)
with Image.open(OUT / 'little-beetle-alert-256.png') as image:
    image.save(OUT / 'little-beetle-alert.ico', sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])

def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'),size)

sheet = Image.new('RGB',(720,430),'#eeeeee')
d = ImageDraw.Draw(sheet)
d.text((22,16),'Beetle with reminder status',font=font(24,True),fill='#222')
for i,(slug,title) in enumerate([('little-beetle','No active reminder'),('little-beetle-alert','Unsnoozed reminder')]):
    x = 18+i*350
    d.rounded_rectangle((x,65,x+334,410),radius=3,fill='#fafafa',outline='#b7b7b7')
    d.text((x+16,80),title,font=font(18,True),fill='#222')
    with Image.open(OUT / f'{slug}-128.png') as im:
        sheet.paste(im,(x+103,112),im)
    for y,bg,fg,label in [(260,'#e8e8e8','#444','Light tray'),(326,'#292c31','#eee','Dark tray')]:
        d.rectangle((x+12,y,x+322,y+49),fill=bg)
        d.text((x+20,y+14),label,font=font(13),fill=fg)
        for j,size in enumerate((16,24,32)):
            px = x+131+j*66
            with Image.open(OUT / f'{slug}-{size}.png') as im:
                sheet.paste(im,(px,y+(49-size)//2),im)
    for j,label in enumerate(('16','24','32 px')):
        d.text((x+133+j*66,386),label,font=font(11),fill='#555')
sheet.save(OUT / 'beetle-alert-preview.png')
print('Saved '+str(OUT / 'beetle-alert-preview.png'))
