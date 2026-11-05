"""Genera aso-adminis.ico (y una vista previa PNG) a partir del glifo de caja registradora.

La geometria es la MISMA que LogoCajaGeometry en Styles/Componentes.xaml (cuadricula 24x24):
si se cambia una, cambiar la otra. Uso:  python generar_icono.py   (requiere Pillow)
"""
from pathlib import Path
from PIL import Image, ImageDraw

AQUI = Path(__file__).resolve().parent
TILE = (0xFF, 0xAD, 0x3E, 255)   # PrimaryButtonColor
TINTA = (0x2B, 0x1C, 0x00, 255)  # OnAccentColor
SS = 8                           # supermuestreo

def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]

def glifo(simple):
    """Devuelve (rellenos, huecos) en coordenadas 24x24."""
    rellenos = [
        rect(13, 2, 20, 6.5),                          # visor del cliente
        rect(15.5, 6.5, 17.5, 9),                      # poste del visor
        [(4, 9), (20, 9), (21.5, 16), (2.5, 16)],      # cuerpo
        rect(1.5, 17, 22.5, 21.5),                     # gaveta
    ]
    if not simple:
        rellenos.append(rect(4.5, 3, 9.5, 9))          # ticket
    if simple:  # 2x2 teclas mas grandes para 16/24 px
        huecos = [rect(x, y, x + 3.5, y + 1.8) for x in (6.5, 14) for y in (10.6, 13.4)]
    else:
        huecos = [rect(x, y, x + 2, y + 1.6) for x in (6, 11, 16) for y in (10.8, 13.2)]
    huecos.append(rect(9, 18.5, 15, 20))               # tirador de la gaveta
    return rellenos, huecos

def render(tam):
    n = tam * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, n - 1, n - 1], radius=n * 0.22, fill=TILE)
    margen = n * (0.06 if tam <= 24 else 0.14)
    esc = (n - 2 * margen) / 24
    rellenos, huecos = glifo(simple=tam <= 24)
    tx = lambda pts: [(margen + x * esc, margen + y * esc) for x, y in pts]
    for p in rellenos:
        d.polygon(tx(p), fill=TINTA)
    for p in huecos:
        d.polygon(tx(p), fill=TILE)
    return img.resize((tam, tam), Image.LANCZOS)

if __name__ == "__main__":
    tamanos = [16, 24, 32, 48, 64, 128, 256]
    imgs = [render(t) for t in tamanos]
    imgs[-1].save(AQUI / "aso-adminis-256.png")
    imgs[-1].save(AQUI / "aso-adminis.ico", sizes=[(t, t) for t in tamanos],
                  append_images=imgs[:-1])
    print("OK:", AQUI / "aso-adminis.ico")
