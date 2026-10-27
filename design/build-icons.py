"""Original layered vector icon studies, no image-generation service or external artwork."""
from pathlib import Path
import json
import subprocess
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'outputs' / 'icons'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = ROOT / 'design' / 'icons'
SOURCE.mkdir(exist_ok=True)

def svg(body, defs=''):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 64 64"><defs>{defs}</defs>{body}</svg>\n'

def bell(fill, stroke, badge='#ce3936', gloss=True):
    return f'''
<g stroke="{stroke}" stroke-width="2.8" stroke-linejoin="round">
 <path d="M28 14v-3a4 4 0 0 1 8 0v3" fill="none"/>
 <circle cx="32" cy="49" r="6" fill="{fill}"/>
 <path d="M13 45c5-5 5-11 5-18a14 14 0 0 1 28 0c0 7 0 13 5 18v3H13z" fill="{fill}"/>
 <path d="M14 43h36" fill="none" opacity=".35"/>
</g>
{('<path d="M23 32v-5c0-6 3-9 6-10" fill="none" stroke="#fff" stroke-width="3" stroke-linecap="round" opacity=".5"/>' if gloss else '')}
<circle cx="48" cy="15" r="10" fill="{badge}" stroke="#fff" stroke-width="2.8"/>
'''

gold_defs = '<linearGradient id="gold" x1="0" y1="0" x2=".7" y2="1"><stop stop-color="#ffe39a"/><stop offset=".55" stop-color="#efb83e"/><stop offset="1" stop-color="#d79420"/></linearGradient>'
blue_defs = '<linearGradient id="blue" x1="0" y1="0" x2=".8" y2="1"><stop stop-color="#9ecbeb"/><stop offset=".55" stop-color="#4e91c4"/><stop offset="1" stop-color="#2f659b"/></linearGradient>'

icons = [
 ('amber-bell', 'Amber bell', 'Classic utility, clear alert dot', svg(bell('url(#gold)', '#73561e'), gold_defs)),
 ('blue-bell', 'Blue bell', 'Cooler Windows-style alternative', svg(bell('url(#blue)', '#29445d'), blue_defs)),
 ('plain-bell', 'Plain bell', 'Simple neutral bell, red alert dot', svg(bell('#dce2e8', '#36404b', gloss=False))),
 ('alarm-clock', 'Alarm clock', 'Emphasizes scheduled reminders', svg('''
 <g stroke="#34495c" stroke-width="2.8" stroke-linejoin="round" stroke-linecap="round">
 <path d="M10 24 6 19C8 8 17 5 24 10l3 5zM54 24l4-5C56 8 47 5 40 10l-3 5z" fill="#6099c3"/>
 <path d="M19 51l-5 8m31-8 5 8M32 5v7" fill="none"/>
 <circle cx="32" cy="34" r="22" fill="#5994c0"/>
 <circle cx="32" cy="34" r="17.5" fill="#fff7df" stroke-width="1.8"/>
 <path d="M32 20v14l10 5" fill="none"/>
 </g><circle cx="32" cy="34" r="2.5" fill="#c63834"/>
 ''')),
 ('checklist', 'Checklist', 'Tasks and completion first', svg('''
 <rect x="13" y="8" width="39" height="49" rx="3" fill="#6099c3" stroke="#34495c" stroke-width="2.8"/>
 <rect x="18" y="13" width="29" height="38" rx="1" fill="#fffdf1"/>
 <rect x="23" y="5" width="19" height="10" rx="2" fill="#cbd3db" stroke="#34495c" stroke-width="2.5"/>
 <g stroke-linecap="round" stroke-linejoin="round" fill="none">
 <path d="m22 25 3 3 5-6m-8 15 3 3 5-6" stroke="#337949" stroke-width="3"/>
 <path d="M35 26h8m-8 11h8m-21 9h21" stroke="#6b7984" stroke-width="2.5"/>
 </g>''')),
 ('little-beetle', 'Little beetle', 'Playful option if Bugger stays', svg('''
 <g stroke="#343c42" stroke-width="3.1" stroke-linecap="round" stroke-linejoin="round">
 <path d="m23 29-9-6m9 16H12m12 9-9 7m26-26 9-6m-9 16h11m-12 9 9 7M27 16l-6-9m16 9 6-9" fill="none"/>
 <path d="M24 24v-5a8 8 0 0 1 16 0v5" fill="#56616b"/>
 <path d="M32 20c-10 0-16 9-16 19 0 13 7 19 16 19s16-6 16-19c0-10-6-19-16-19z" fill="#d8a442"/>
 <path d="M32 22v34" fill="none"/>
 </g><path d="M23 30c-2 4-3 9-2 13" stroke="#ffe4a7" stroke-width="3" stroke-linecap="round" fill="none"/>
 ''')),
]

for slug, label, subtitle, content in icons:
    (OUT / f'{slug}.svg').write_text(content, encoding='utf-8')
    (SOURCE / f'{slug}.svg').write_text(content, encoding='utf-8')

subprocess.run(['node', str(ROOT / 'design' / 'render-icons.cjs'), str(OUT)], check=True)

for slug, *_ in icons:
    with Image.open(OUT / f'{slug}-256.png') as image:
        image.save(OUT / f'{slug}.ico', sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])

def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/' + ('segoeuib.ttf' if bold else 'segoeui.ttf'), size)

sheet = Image.new('RGB', (800, 870), '#e9e9e9')
draw = ImageDraw.Draw(sheet)
draw.text((22, 16), 'Application icon studies', font=font(25, True), fill='#222')
draw.text((22, 51), 'Original vector layers. Transparent assets. Real tray-size previews.', font=font(15), fill='#555')

for index, (slug, label, subtitle, _) in enumerate(icons):
    x, y = 18+(index%2)*392, 90+(index//2)*254
    draw.rounded_rectangle((x,y,x+372,y+238), radius=3, fill='#f8f8f8', outline='#b6b6b6')
    with Image.open(OUT / f'{slug}-64.png') as im:
        sheet.paste(im, (x+18,y+25), im)
    draw.text((x+98,y+24), label, font=font(20,True), fill='#222')
    draw.text((x+98,y+56), subtitle, font=font(13), fill='#555')
    for offset,bg,fg,title in [(105,'#eeeeee','#444','Light tray'),(163,'#292c31','#eee','Dark tray')]:
        draw.rectangle((x+12,y+offset,x+360,y+offset+49), fill=bg)
        draw.text((x+21,y+offset+13), title, font=font(13), fill=fg)
        for i,size in enumerate([16,24,32]):
            px=x+143+i*71
            with Image.open(OUT / f'{slug}-{size}.png') as im:
                sheet.paste(im,(px,y+offset+(49-size)//2),im)
    draw.text((x+150,y+216),'16',font=font(11),fill='#555')
    draw.text((x+220,y+216),'24',font=font(11),fill='#555')
    draw.text((x+291,y+216),'32 px',font=font(11),fill='#555')

sheet.save(OUT / 'icon-options.png')
(OUT / 'README.txt').write_text('Original icon studies built from editable SVG shapes.\nNo external artwork or image-generation model was used.\nEach .ico includes 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixel versions.\nPNG and SVG files have transparent backgrounds.\nThese are proposals only; no icon has been installed in the application yet.\nRebuild with: python design/build-icons.py\n', encoding='utf-8')
print('Saved ' + str(OUT / 'icon-options.png'))
