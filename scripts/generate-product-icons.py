#!/usr/bin/env python3
"""Generate flat SVG icons for all 501 master products."""

import json
import os

TEMPLATE_PATH = os.path.join(os.path.dirname(__file__), '..', 'src', 'Famick.HomeManagement.Infrastructure', 'Data', 'SeedData', 'product-templates.json')
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), '..', 'src', 'Famick.HomeManagement.UI', 'wwwroot', 'images', 'master-products')

def svg_wrap(inner):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">{inner}</svg>'

# ── Category color palettes ──────────────────────────────────────────
PALETTES = {
    'Baby':            ('#E0F2FE', '#7DD3FC', '#38BDF8'),
    'Bakery':          ('#FEF3C7', '#D97706', '#92400E'),
    'Baking':          ('#FDE68A', '#F59E0B', '#78350F'),
    'Beverages':       ('#DBEAFE', '#3B82F6', '#1E40AF'),
    'Breakfast':       ('#FEF9C3', '#EAB308', '#A16207'),
    'Canned Goods':    ('#E5E7EB', '#6B7280', '#374151'),
    'Condiments':      ('#FEE2E2', '#EF4444', '#991B1B'),
    'Dairy':           ('#EFF6FF', '#93C5FD', '#2563EB'),
    'Deli':            ('#FCE7F3', '#EC4899', '#9D174D'),
    'Frozen':          ('#E0E7FF', '#818CF8', '#4338CA'),
    'Grains & Pasta':  ('#FEF3C7', '#D4A054', '#92400E'),
    'Household':       ('#D1FAE5', '#34D399', '#065F46'),
    'International':   ('#FDE68A', '#F97316', '#9A3412'),
    'Kitchen Supplies':('#F3F4F6', '#9CA3AF', '#4B5563'),
    'Meat & Seafood':  ('#FEE2E2', '#F87171', '#991B1B'),
    'Pantry':          ('#FEF3C7', '#CA8A04', '#713F12'),
    'Personal Care':   ('#F3E8FF', '#A78BFA', '#6D28D9'),
    'Pet':             ('#DCFCE7', '#4ADE80', '#15803D'),
    'Pharmacy':        ('#ECFDF5', '#6EE7B7', '#047857'),
    'Produce':         ('#DCFCE7', '#4ADE80', '#15803D'),
}

# ── SVG shape builders ───────────────────────────────────────────────

def _circle(cx, cy, r, fill):
    return f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}"/>'

def _rect(x, y, w, h, fill, rx=0):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}"/>'

def _ellipse(cx, cy, rx, ry, fill):
    return f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{fill}"/>'

def _path(d, fill):
    return f'<path d="{d}" fill="{fill}"/>'

def _line(x1, y1, x2, y2, stroke, sw=2):
    return f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{stroke}" stroke-width="{sw}" stroke-linecap="round"/>'

def _polygon(points, fill):
    return f'<polygon points="{points}" fill="{fill}"/>'

# ── Generic category shapes ──────────────────────────────────────────

def icon_jar(bg, fg, accent):
    """Baby food jar / generic jar."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(20,10,24,6,accent,2) +
        _rect(18,16,28,38,fg,4) +
        _rect(24,24,16,10,accent,2)
    )

def icon_bottle(bg, fg, accent):
    """Generic bottle shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(26,8,12,8,accent,2) +
        _rect(22,16,20,40,fg,4) +
        _rect(26,20,12,8,accent,2)
    )

def icon_can(bg, fg, accent):
    """Cylindrical can."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,14,28,36,fg,4) +
        _ellipse(32,14,14,4,accent) +
        _ellipse(32,50,14,4,accent) +
        _rect(24,24,16,8,accent,2)
    )

def icon_box(bg, fg, accent):
    """Generic box/carton."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(16,12,32,40,fg,4) +
        _rect(16,12,32,10,accent,2) +
        _rect(22,28,20,16,accent,3)
    )

def icon_bag(bg, fg, accent):
    """Generic bag shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M18 52 L22 12 L42 12 L46 52 Z', fg) +
        _rect(22,12,20,6,accent,2) +
        _rect(26,26,12,12,accent,2)
    )

def icon_loaf(bg, fg, accent):
    """Bread loaf shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M12 44 Q12 20 32 16 Q52 20 52 44 Z', fg) +
        _rect(12,40,40,12,fg,0) +
        _line(22,26,22,44,accent,2) +
        _line(32,22,32,44,accent,2) +
        _line(42,26,42,44,accent,2)
    )

def icon_round_fruit(bg, fg, accent):
    """Round fruit (apple, orange, etc.)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(32,36,18,fg) +
        _path('M32 18 Q36 10 40 14', accent) +
        _ellipse(28,30,4,6,accent)
    )

def icon_long_veggie(bg, fg, accent):
    """Long vegetable (carrot, celery, etc.)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M24 12 Q20 32 28 56 L36 56 Q40 32 36 12 Z', fg) +
        _path('M28 8 Q32 4 36 8 L34 16 L26 16 Z', accent)
    )

def icon_leafy(bg, fg, accent):
    """Leafy green."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,30,20,16,fg) +
        _ellipse(26,34,14,12,accent) +
        _line(32,30,32,56,fg,3)
    )

def icon_meat_cut(bg, fg, accent):
    """Meat cut / steak."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,34,22,16,fg) +
        _ellipse(28,32,6,4,accent) +
        _path('M14 28 Q32 18 50 28 Q50 40 32 50 Q14 40 14 28 Z', fg)
    )

def icon_fish(bg, fg, accent):
    """Fish shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M10 32 Q20 16 40 20 L54 32 L40 44 Q20 48 10 32 Z', fg) +
        _circle(22,30,3,accent) +
        _path('M54 32 L62 24 L62 40 Z', accent)
    )

def icon_cup(bg, fg, accent):
    """Cup/mug shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M16 16 L20 52 L44 52 L48 16 Z', fg) +
        _rect(16,16,32,6,accent,2) +
        _path('M48 24 Q58 24 58 34 Q58 44 48 44', accent)
    )

def icon_spray(bg, fg, accent):
    """Spray bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(22,24,20,34,fg,3) +
        _rect(24,10,8,14,accent,2) +
        _path('M32 10 L42 6 L44 10 L36 14 Z', accent) +
        _rect(26,30,12,8,accent,2)
    )

def icon_roll(bg, fg, accent):
    """Paper roll (toilet paper, paper towels)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,14,28,40,fg,4) +
        _ellipse(32,14,14,6,accent) +
        _ellipse(32,54,14,6,accent) +
        _circle(32,14,5,bg)
    )

def icon_pill(bg, fg, accent):
    """Pill/capsule."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(14,26,36,12,fg,6) +
        _rect(32,26,18,12,accent,0) +
        # round ends
        _circle(14,32,6,fg) +
        _circle(50,32,6,accent)
    )

def icon_pet_bowl(bg, fg, accent):
    """Pet food bowl."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,44,24,10,fg) +
        _path('M10 38 Q10 28 32 24 Q54 28 54 38 L50 44 L14 44 Z', fg) +
        _ellipse(32,30,12,6,accent) +
        _circle(24,30,3,accent) +
        _circle(40,30,3,accent)
    )

def icon_egg(bg, fg, accent):
    """Egg shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M32 10 Q48 20 48 38 Q48 54 32 56 Q16 54 16 38 Q16 20 32 10 Z', fg) +
        _ellipse(28,32,6,8,accent)
    )

def icon_cheese(bg, fg, accent):
    """Cheese wedge."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _polygon('12,48 52,48 52,24', fg) +
        _rect(12,48,40,8,accent,0) +
        _circle(30,40,3,accent) +
        _circle(40,38,2,accent) +
        _circle(36,44,2,accent)
    )

def icon_yogurt(bg, fg, accent):
    """Yogurt cup."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M18 18 L22 52 L42 52 L46 18 Z', fg) +
        _ellipse(32,18,14,4,accent) +
        _rect(24,28,16,10,accent,3)
    )

def icon_milk(bg, fg, accent):
    """Milk carton."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,20,28,36,fg,3) +
        _polygon('18,20 32,8 46,20', accent) +
        _rect(24,32,16,12,accent,2)
    )

def icon_butter(bg, fg, accent):
    """Butter stick."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(10,24,44,20,fg,3) +
        _rect(10,24,44,6,accent,0) +
        _line(30,30,30,44,accent,2)
    )

def icon_pizza(bg, fg, accent):
    """Pizza slice."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _polygon('32,8 8,56 56,56', fg) +
        _circle(28,36,4,accent) +
        _circle(36,40,3,accent) +
        _circle(32,50,3,accent)
    )

def icon_ice_cream(bg, fg, accent):
    """Ice cream cone."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _polygon('24,34 40,34 32,58', accent) +
        _circle(32,26,12,fg) +
        _circle(26,28,4,bg)
    )

def icon_fries(bg, fg, accent):
    """French fries."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M18 32 L22 56 L42 56 L46 32 Z', fg) +
        _rect(22,14,4,24,accent,1) +
        _rect(28,10,4,28,accent,1) +
        _rect(34,12,4,26,accent,1) +
        _rect(40,16,4,22,accent,1)
    )

def icon_nuggets(bg, fg, accent):
    """Chicken nuggets."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(24,28,10,8,fg) +
        _ellipse(40,28,10,8,fg) +
        _ellipse(24,42,10,8,fg) +
        _ellipse(40,42,10,8,accent)
    )

def icon_shrimp(bg, fg, accent):
    """Shrimp shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M44 16 Q56 20 56 32 Q56 44 44 48 Q32 52 24 44 Q20 38 24 32 Q28 26 36 24', fg) +
        _path('M44 16 L48 10 M44 16 L40 10', accent) +
        _circle(48,24,2,accent)
    )

def icon_stir_fry(bg, fg, accent):
    """Stir fry pan."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(30,40,22,12,fg) +
        _line(52,40,62,32,accent,3) +
        _circle(24,36,3,accent) +
        _circle(32,34,3,accent) +
        _circle(36,40,3,accent)
    )

def icon_popsicle(bg, fg, accent):
    """Ice pop."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(24,8,16,34,fg,6) +
        _rect(24,24,16,18,accent,0) +
        _rect(30,42,4,16,accent,1)
    )

def icon_pie(bg, fg, accent):
    """Pie / pie crust."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,40,24,12,fg) +
        _path('M8 40 Q8 28 32 24 Q56 28 56 40', accent) +
        _path('M16 36 L32 28 L48 36', fg)
    )

def icon_meatball(bg, fg, accent):
    """Meatballs."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(22,30,10,fg) +
        _circle(42,30,10,fg) +
        _circle(32,46,10,fg) +
        _circle(22,30,4,accent) +
        _circle(42,30,4,accent) +
        _circle(32,46,4,accent)
    )

def icon_burrito(bg, fg, accent):
    """Burrito / wrap."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M12 44 L28 12 L52 20 L36 52 Z', fg) +
        _line(20,28,44,36,accent,2) +
        _circle(30,30,3,accent) +
        _circle(38,38,3,accent)
    )

def icon_waffle(bg, fg, accent):
    """Waffle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(12,18,40,28,fg,4) +
        # grid
        _line(12,26,52,26,accent,1) +
        _line(12,34,52,34,accent,1) +
        _line(24,18,24,46,accent,1) +
        _line(36,18,36,46,accent,1) +
        _ellipse(32,14,8,4,accent)
    )

def icon_cereal(bg, fg, accent):
    """Cereal bowl."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,42,24,12,fg) +
        _path('M8 36 Q8 24 32 20 Q56 24 56 36', fg) +
        _circle(24,30,3,accent) +
        _circle(32,28,3,accent) +
        _circle(40,30,3,accent) +
        _circle(28,34,3,accent) +
        _circle(36,34,3,accent)
    )

def icon_granola_bar(bg, fg, accent):
    """Granola / breakfast bar."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(8,22,48,20,fg,4) +
        _rect(8,22,48,6,accent,0) +
        _circle(20,34,2,accent) +
        _circle(32,36,2,accent) +
        _circle(44,34,2,accent)
    )

def icon_pancake(bg, fg, accent):
    """Pancake stack."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,46,22,6,fg) +
        _ellipse(32,38,22,6,fg) +
        _ellipse(32,30,22,6,fg) +
        _path('M24 20 Q28 10 32 14 Q36 10 40 20', accent) +  # butter drip
        _ellipse(32,24,6,3,accent)
    )

def icon_oatmeal(bg, fg, accent):
    """Bowl of oatmeal."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,44,24,10,fg) +
        _path('M8 38 Q8 26 32 22 Q56 26 56 38', fg) +
        _path('M16 30 Q24 26 32 30 Q40 26 48 30', accent)
    )

def icon_toaster_pastry(bg, fg, accent):
    """Toaster pastry / pop-tart."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(14,10,36,44,fg,6) +
        _rect(18,16,28,32,accent,4) +
        _circle(26,28,2,bg) +
        _circle(34,32,2,bg) +
        _circle(30,38,2,bg)
    )

def icon_dressing_bottle(bg, fg, accent):
    """Salad dressing / sauce bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(28,6,8,8,accent,2) +
        _path('M22 14 L20 52 L44 52 L42 14 Z', fg) +
        _rect(24,24,16,12,accent,3)
    )

def icon_olive(bg, fg, accent):
    """Olive."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,34,14,18,fg) +
        _circle(32,30,5,accent) +
        _path('M30 14 Q32 8 34 14', accent)
    )

def icon_noodles(bg, fg, accent):
    """Noodle/pasta shape."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M12 24 Q22 16 32 24 Q42 32 52 24', fg) +
        _path('M12 32 Q22 24 32 32 Q42 40 52 32', fg) +
        _path('M12 40 Q22 32 32 40 Q42 48 52 40', accent)
    )

def icon_rice_bag(bg, fg, accent):
    """Rice bag."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M16 50 L20 14 L44 14 L48 50 Z', fg) +
        _rect(20,14,24,6,accent,2) +
        _circle(28,32,2,accent) +
        _circle(36,36,2,accent) +
        _circle(32,42,2,accent) +
        _circle(24,40,2,accent) +
        _circle(40,30,2,accent)
    )

def icon_tube(bg, fg, accent):
    """Tube (toothpaste, cream)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(20,20,24,34,fg,4) +
        _path('M26 20 L32 8 L38 20 Z', accent) +
        _rect(24,30,16,10,accent,2)
    )

def icon_soap(bg, fg, accent):
    """Soap bar / dispenser."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,28,28,24,fg,4) +
        _rect(28,16,8,12,accent,2) +
        _circle(24,16,3,accent) +
        _circle(32,10,2,accent) +
        _circle(40,14,2,accent)
    )

def icon_razor(bg, fg, accent):
    """Razor."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(28,14,8,40,fg,2) +
        _rect(18,10,28,10,accent,3) +
        _line(22,14,22,20,bg,1) +
        _line(32,14,32,20,bg,1) +
        _line(42,14,42,20,bg,1)
    )

def icon_bandage(bg, fg, accent):
    """Bandage / band-aid."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(6,24,52,16,fg,8) +
        _rect(22,24,20,16,accent,0) +
        _circle(28,32,2,fg) +
        _circle(36,32,2,fg)
    )

def icon_thermometer(bg, fg, accent):
    """Thermometer."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(28,8,8,36,fg,4) +
        _circle(32,50,10,fg) +
        _circle(32,50,6,accent) +
        _rect(30,24,4,20,accent,0)
    )

def icon_vitamin(bg, fg, accent):
    """Vitamin bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(20,18,24,38,fg,4) +
        _rect(24,10,16,8,accent,2) +
        _rect(24,28,16,6,accent,2) +
        _circle(32,44,4,accent)
    )

def icon_battery(bg, fg, accent):
    """Battery."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(14,18,32,28,fg,3) +
        _rect(46,26,6,12,accent,1) +
        _rect(18,22,12,20,accent,2) +
        _line(24,28,24,36,bg,2) +
        _line(20,32,28,32,bg,2)
    )

def icon_candle(bg, fg, accent):
    """Candle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(22,24,20,32,fg,3) +
        _rect(30,18,4,6,accent,0) +
        _path('M32 8 Q38 14 32 18 Q26 14 32 8 Z', accent)
    )

def icon_sponge(bg, fg, accent):
    """Sponge."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(12,20,40,28,fg,6) +
        _circle(22,30,3,accent) +
        _circle(32,34,3,accent) +
        _circle(42,30,3,accent) +
        _circle(27,40,2,accent) +
        _circle(37,40,2,accent)
    )

def icon_foil_roll(bg, fg, accent):
    """Foil / wrap roll."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(8,20,48,24,fg,4) +
        _ellipse(8,32,4,12,accent) +
        _ellipse(56,32,4,12,accent) +
        _path('M56 32 L62 28 M56 32 L64 36', accent)
    )

def icon_plate(bg, fg, accent):
    """Plate / disposable plate."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,36,26,14,fg) +
        _ellipse(32,34,18,10,accent) +
        _ellipse(32,33,10,6,bg)
    )

def icon_napkin(bg, fg, accent):
    """Napkin."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(14,14,36,36,fg,2) +
        _polygon('14,14 50,14 14,50', accent)
    )

def icon_gloves(bg, fg, accent):
    """Gloves."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M20 56 L20 28 L16 16 L20 14 L24 24 L24 14 L28 12 L28 24 L32 14 L36 12 L36 24 L40 16 L44 18 L38 28 L38 56 Z', fg) +
        _rect(18,48,22,8,accent,2)
    )

def icon_cutlery(bg, fg, accent):
    """Fork and knife."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        # fork
        _rect(18,8,2,20,fg,1) +
        _rect(22,8,2,20,fg,1) +
        _rect(26,8,2,20,fg,1) +
        _rect(20,28,6,28,fg,2) +
        # knife
        _path('M38 8 L44 8 L44 32 L38 36 Z', accent) +
        _rect(38,36,6,20,accent,2)
    )

def icon_taco(bg, fg, accent):
    """Taco shell."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M8 44 Q32 4 56 44 Z', fg) +
        _circle(24,36,4,accent) +
        _circle(32,32,4,accent) +
        _circle(40,36,4,accent)
    )

def icon_curry(bg, fg, accent):
    """Curry paste jar."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,22,28,32,fg,4) +
        _rect(22,14,20,8,accent,2) +
        _path('M24 34 Q32 28 40 34 Q32 40 24 34 Z', accent)
    )

def icon_noodle_pack(bg, fg, accent):
    """Noodle package (ramen, soba, rice noodles)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(12,14,40,36,fg,4) +
        _path('M18 30 Q28 22 38 30 Q48 38 52 30', accent) +
        _path('M18 38 Q28 30 38 38 Q48 46 52 38', accent)
    )

def icon_chips(bg, fg, accent):
    """Chips / pita chips bag."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M16 52 L20 10 L44 10 L48 52 Z', fg) +
        _path('M20 10 Q32 6 44 10', accent) +
        _polygon('26,28 32,20 38,28', accent) +
        _polygon('22,40 28,32 34,40', accent)
    )

def icon_deli_meat(bg, fg, accent):
    """Deli meat slices."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(30,38,18,14,fg) +
        _ellipse(34,34,18,14,fg) +
        _ellipse(32,30,18,14,accent)
    )

def icon_salad(bg, fg, accent):
    """Salad / coleslaw bowl."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,44,24,10,fg) +
        _path('M8 38 Q8 26 32 22 Q56 26 56 38', fg) +
        _circle(24,30,4,accent) +
        _circle(34,28,5,accent) +
        _circle(42,32,3,accent)
    )

def icon_hummus(bg, fg, accent):
    """Hummus tub."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,42,24,12,fg) +
        _path('M10 36 Q10 24 32 20 Q54 24 54 36', fg) +
        _ellipse(32,30,12,6,accent) +
        _circle(32,30,4,bg)
    )

def icon_chocolate(bg, fg, accent):
    """Chocolate bar / chips."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(12,16,40,32,fg,4) +
        _line(12,28,52,28,accent,1) +
        _line(12,40,52,40,accent,1) +
        _line(24,16,24,48,accent,1) +
        _line(36,16,36,48,accent,1)
    )

def icon_sugar(bg, fg, accent):
    """Sugar bag."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M16 52 L18 12 L46 12 L48 52 Z', fg) +
        _rect(18,12,28,8,accent,2) +
        _ellipse(32,38,10,6,accent)
    )

def icon_flour_bag(bg, fg, accent):
    """Flour bag."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M14 54 L18 10 L46 10 L50 54 Z', fg) +
        _path('M18 10 Q32 4 46 10', accent) +
        _rect(24,24,16,14,accent,3)
    )

def icon_extract_bottle(bg, fg, accent):
    """Small extract bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(24,22,16,32,fg,3) +
        _rect(28,10,8,12,accent,2) +
        _rect(28,30,8,8,accent,2)
    )

def icon_oil_bottle(bg, fg, accent):
    """Oil bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M24 20 L22 54 L42 54 L40 20 Z', fg) +
        _rect(28,8,8,12,accent,2) +
        _ellipse(32,40,8,10,accent)
    )

def icon_honey_jar(bg, fg, accent):
    """Honey jar."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,22,28,32,fg,6) +
        _rect(22,14,20,8,accent,2) +
        _path('M26 34 Q32 28 38 34 Q32 40 26 34 Z', accent)
    )

def icon_peanut_butter(bg, fg, accent):
    """Peanut butter jar."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,20,28,34,fg,4) +
        _rect(22,12,20,8,accent,2) +
        _ellipse(32,38,8,6,accent)
    )

def icon_vinegar(bg, fg, accent):
    """Vinegar bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M24 22 L22 54 L42 54 L40 22 Z', fg) +
        _rect(28,8,8,14,accent,2) +
        _rect(26,36,12,8,accent,2)
    )

def icon_broth_box(bg, fg, accent):
    """Broth carton."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,10,28,44,fg,3) +
        _polygon('18,10 32,4 46,10', accent) +
        _rect(24,24,16,16,accent,2) +
        _path('M28 30 Q32 26 36 30 Q32 34 28 30 Z', bg)
    )

def icon_syrup(bg, fg, accent):
    """Syrup bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M24 20 L22 54 L42 54 L40 20 Z', fg) +
        _rect(28,8,8,12,accent,2) +
        _rect(20,44,24,6,accent,2)
    )

def icon_jam_jar(bg, fg, accent):
    """Jam jar."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(18,22,28,32,fg,5) +
        _rect(22,14,20,8,accent,2) +
        _circle(28,38,4,accent) +
        _circle(36,36,3,accent)
    )

def icon_cat(bg, fg, accent):
    """Cat face."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(32,36,18,fg) +
        _polygon('16,22 14,6 26,18', fg) +
        _polygon('48,22 50,6 38,18', fg) +
        _circle(26,32,3,accent) +
        _circle(38,32,3,accent) +
        _polygon('32,38 30,42 34,42', accent)
    )

def icon_dog(bg, fg, accent):
    """Dog face."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(32,36,18,fg) +
        _ellipse(16,26,8,12,fg) +
        _ellipse(48,26,8,12,fg) +
        _circle(26,32,3,accent) +
        _circle(38,32,3,accent) +
        _ellipse(32,40,6,4,accent)
    )

def icon_waste_bag(bg, fg, accent):
    """Pet waste bag."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M20 52 L18 16 L46 16 L44 52 Z', fg) +
        _path('M18 16 Q32 8 46 16', accent) +
        _circle(32,36,6,accent)
    )

def icon_diaper(bg, fg, accent):
    """Diaper."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M12 20 Q32 10 52 20 L48 48 Q32 54 16 48 Z', fg) +
        _ellipse(32,34,10,8,accent) +
        _path('M20 22 L20 18 M44 22 L44 18', accent)
    )

def icon_wipes(bg, fg, accent):
    """Wipes container."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(12,22,40,30,fg,4) +
        _rect(12,22,40,8,accent,2) +
        _path('M28 22 Q32 14 36 22', accent)
    )

def icon_baby_bottle(bg, fg, accent):
    """Baby bottle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(24,22,16,32,fg,4) +
        _rect(28,10,8,12,accent,2) +
        _path('M28 10 Q32 4 36 10', fg) +
        _rect(24,34,16,8,accent,2)
    )

def icon_corn(bg, fg, accent):
    """Corn cob."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,32,12,22,fg) +
        _circle(28,24,3,accent) + _circle(36,24,3,accent) +
        _circle(28,32,3,accent) + _circle(36,32,3,accent) +
        _circle(28,40,3,accent) + _circle(36,40,3,accent) +
        _path('M20 42 Q16 52 12 56', accent) +
        _path('M44 42 Q48 52 52 56', accent)
    )

def icon_mushroom(bg, fg, accent):
    """Mushroom."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(28,32,8,22,accent) +
        _path('M10 34 Q10 14 32 10 Q54 14 54 34 Z', fg) +
        _circle(24,24,3,accent) +
        _circle(38,20,4,accent)
    )

def icon_onion(bg, fg, accent):
    """Onion."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M32 12 Q50 20 50 40 Q50 54 32 56 Q14 54 14 40 Q14 20 32 12 Z', fg) +
        _path('M32 8 L32 14 M28 10 L32 14 M36 10 L32 14', accent) +
        _path('M22 30 Q32 24 42 30', accent)
    )

def icon_potato(bg, fg, accent):
    """Potato."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,34,22,16,fg) +
        _circle(24,30,2,accent) +
        _circle(36,28,2,accent) +
        _circle(40,36,2,accent) +
        _circle(28,38,2,accent)
    )

def icon_tomato(bg, fg, accent):
    """Tomato."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(32,36,18,fg) +
        _path('M26 18 Q32 12 38 18', accent) +
        _path('M32 18 L32 12', accent)
    )

def icon_pepper(bg, fg, accent):
    """Bell pepper."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M20 22 Q16 40 20 52 L44 52 Q48 40 44 22 Z', fg) +
        _path('M20 22 Q28 16 32 22 Q36 16 44 22', fg) +
        _rect(30,10,4,12,accent,1)
    )

def icon_berry(bg, fg, accent):
    """Berry cluster."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(26,34,8,fg) +
        _circle(38,34,8,fg) +
        _circle(32,44,8,fg) +
        _circle(32,26,8,accent) +
        _path('M30 16 Q32 8 34 16', accent)
    )

def icon_banana(bg, fg, accent):
    """Banana."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M14 44 Q10 20 32 12 Q54 12 52 28 Q48 36 24 48 Z', fg) +
        _path('M52 28 Q56 24 54 18', accent) +
        _line(14,44,18,48,accent,2)
    )

def icon_citrus(bg, fg, accent):
    """Citrus fruit (lemon, lime, orange, grapefruit)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(32,34,18,fg) +
        _path('M32 16 L34 12', accent) +
        _circle(32,34,10,accent) +
        _line(32,24,32,44,bg,1) +
        _line(22,34,42,34,bg,1) +
        _line(25,27,39,41,bg,1) +
        _line(39,27,25,41,bg,1)
    )

def icon_melon(bg, fg, accent):
    """Melon (cantaloupe, watermelon)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(32,34,20,fg) +
        _path('M12 34 Q32 30 52 34 Q32 38 12 34 Z', accent) +
        _path('M32 14 Q36 34 32 54', accent) +
        _path('M18 20 Q32 24 46 20', accent)
    )

def icon_grape(bg, fg, accent):
    """Grape cluster."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(28,28,6,fg) +
        _circle(38,28,6,fg) +
        _circle(24,38,6,fg) +
        _circle(34,38,6,fg) +
        _circle(44,38,6,fg) +
        _circle(30,48,6,accent) +
        _circle(40,48,6,accent) +
        _path('M32 22 Q34 14 38 12', accent)
    )

def icon_avocado(bg, fg, accent):
    """Avocado."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M32 8 Q52 24 48 44 Q44 58 32 58 Q20 58 16 44 Q12 24 32 8 Z', fg) +
        _ellipse(32,38,10,12,accent) +
        _circle(32,38,5,bg)
    )

def icon_herb_sprig(bg, fg, accent):
    """Herb sprig (basil, cilantro, mint, dill, etc.)."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _line(32,56,32,20,fg,3) +
        _ellipse(24,24,8,6,fg) +
        _ellipse(40,28,8,6,fg) +
        _ellipse(24,36,8,6,accent) +
        _ellipse(40,40,8,6,accent) +
        _ellipse(32,16,6,5,fg)
    )

def icon_pear_shape(bg, fg, accent):
    """Pear-shaped fruit."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M32 10 Q36 10 38 16 Q46 26 46 40 Q46 56 32 56 Q18 56 18 40 Q18 26 26 16 Q28 10 32 10 Z', fg) +
        _path('M32 10 L34 6', accent) +
        _ellipse(28,36,4,6,accent)
    )

def icon_coconut(bg, fg, accent):
    """Coconut."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _circle(32,36,18,fg) +
        _circle(26,30,3,accent) +
        _circle(38,30,3,accent) +
        _circle(32,38,3,accent) +
        _path('M14 36 L50 36', bg)
    )

def icon_ginger(bg, fg, accent):
    """Ginger root."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M16 32 Q20 20 32 24 Q40 16 48 24 Q52 32 44 36 Q40 44 32 40 Q24 44 16 32 Z', fg) +
        _path('M24 28 Q28 24 32 28', accent) +
        _path('M36 24 Q40 20 44 24', accent)
    )

def icon_asparagus(bg, fg, accent):
    """Asparagus bundle."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _rect(22,16,4,40,fg,1) +
        _rect(30,12,4,44,fg,1) +
        _rect(38,18,4,38,fg,1) +
        _polygon('22,16 24,8 26,16', accent) +
        _polygon('30,12 32,4 34,12', accent) +
        _polygon('38,18 40,10 42,18', accent) +
        _rect(18,42,28,4,accent,1)
    )

def icon_eggplant(bg, fg, accent):
    """Eggplant."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M32 14 Q48 22 48 40 Q48 56 32 58 Q16 56 16 40 Q16 22 32 14 Z', fg) +
        _path('M26 14 Q32 6 38 14', accent) +
        _ellipse(28,34,4,8,accent)
    )

def icon_kiwi(bg, fg, accent):
    """Kiwi fruit."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _ellipse(32,34,18,14,fg) +
        _ellipse(32,34,12,10,accent) +
        _circle(32,34,4,bg) +
        _line(32,24,32,22,fg,1) +
        _line(32,44,32,46,fg,1) +
        _line(22,34,20,34,fg,1) +
        _line(42,34,44,34,fg,1)
    )

def icon_mango(bg, fg, accent):
    """Mango."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M32 10 Q52 18 52 36 Q52 54 32 56 Q12 54 12 36 Q12 18 32 10 Z', fg) +
        _path('M30 10 L28 6', accent) +
        _ellipse(26,32,6,10,accent)
    )

def icon_peas(bg, fg, accent):
    """Pea pod."""
    return svg_wrap(
        _rect(0,0,64,64,bg,8) +
        _path('M8 32 Q32 12 56 32 Q32 52 8 32 Z', fg) +
        _circle(20,32,5,accent) +
        _circle(32,32,5,accent) +
        _circle(44,32,5,accent)
    )

# ── Product → icon mapping ───────────────────────────────────────────

def get_icon(product):
    """Return SVG string for a product based on name/category/slug."""
    name = product['name'].lower()
    slug = product['imageSlug']
    cat = product['category']
    bg, fg, accent = PALETTES.get(cat, ('#F3F4F6', '#9CA3AF', '#4B5563'))

    # ── Baby ──
    if cat == 'Baby':
        if 'food' in name:
            return icon_jar('#E0F2FE', '#7DD3FC', '#F59E0B' if 'carrot' in name else '#EF4444' if 'applesauce' in name else '#38BDF8')
        if 'formula' in name:
            if 'powder' in name:
                return icon_can('#E0F2FE', '#7DD3FC', '#38BDF8')
            return icon_box('#E0F2FE', '#7DD3FC', '#38BDF8')
        if 'shampoo' in name:
            return icon_bottle('#E0F2FE', '#7DD3FC', '#FCD34D')
        if 'wipes' in name:
            return icon_wipes('#E0F2FE', '#7DD3FC', '#38BDF8')
        if 'diaper' in name:
            return icon_diaper('#E0F2FE', '#BAE6FD', '#38BDF8')
        return icon_baby_bottle(bg, fg, accent)

    # ── Bakery ──
    if cat == 'Bakery':
        if 'bagel' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _circle(32,34,18,fg) +
                _circle(32,34,7,bg) +
                _ellipse(32,34,18,14,fg)
            )
        if 'croissant' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _path('M10 44 Q20 16 32 20 Q44 16 54 44 Q32 36 10 44 Z', fg) +
                _path('M20 36 Q32 28 44 36', accent)
            )
        if 'roll' in name or 'bun' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _path('M12 44 Q12 22 32 18 Q52 22 52 44 Z', fg) +
                _rect(12,40,40,8,accent,0) +
                _line(32,22,32,44,accent,1)
            )
        if 'donut' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _ellipse(32,34,20,16,fg) +
                _ellipse(32,34,8,6,bg) +
                _ellipse(32,28,18,10,'#F59E0B' if 'glazed' in name else accent)
            )
        if 'english muffin' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _ellipse(32,34,20,12,fg) +
                _ellipse(32,30,18,8,accent) +
                _circle(24,30,2,bg) + _circle(32,28,2,bg) + _circle(40,30,2,bg)
            )
        if 'muffin' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _path('M14 38 Q14 20 32 14 Q50 20 50 38', fg if 'blueberry' not in name else '#6366F1') +
                _rect(12,38,40,16,accent,4) +
                _circle(26,28,3,accent if 'blueberry' not in name else '#4338CA') +
                _circle(36,24,3,accent if 'chocolate' not in name else '#78350F')
            )
        if 'pita' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _ellipse(32,36,22,16,fg) +
                _ellipse(32,34,16,10,accent)
            )
        if 'tortilla' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _circle(32,34,20,fg) +
                _circle(32,34,14,accent) +
                _circle(32,34,8,bg)
            )
        # default: bread loaf
        return icon_loaf(bg, fg, accent)

    # ── Baking ──
    if cat == 'Baking':
        if 'flour' in name:
            return icon_flour_bag(bg, fg, accent)
        if 'sugar' in name:
            return icon_sugar(bg, fg, '#FBBF24' if 'brown' in name else '#E5E7EB' if 'powdered' in name else accent)
        if 'chocolate chip' in name or 'chip' in name:
            return icon_chocolate(bg, '#5B21B6' if 'dark' in name else '#92400E' if 'semi' in name else '#D97706', accent)
        if 'chocolate' in name or 'cocoa' in name:
            return icon_chocolate(bg, '#78350F', '#D97706')
        if 'yeast' in name:
            return icon_bag(bg, fg, accent)
        if 'baking powder' in name or 'baking soda' in name:
            return icon_can(bg, fg, accent)
        if 'cornstarch' in name:
            return icon_box(bg, fg, accent)
        if 'gelatin' in name:
            return icon_box(bg, '#F3F4F6', accent)
        if 'sprinkles' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _rect(22,16,20,36,fg,4) +
                _rect(26,8,12,8,accent,2) +
                _rect(26,22,2,6,'#EF4444',1) +
                _rect(30,26,2,6,'#3B82F6',1) +
                _rect(34,22,2,6,'#10B981',1) +
                _rect(38,28,2,6,'#F59E0B',1)
            )
        if 'pudding' in name:
            return icon_box(bg, fg, '#78350F' if 'chocolate' in name else '#FDE68A')
        if 'brownie' in name or 'cake mix' in name:
            return icon_box(bg, fg, '#78350F' if 'chocolate' in name else '#FBBF24')
        return icon_bag(bg, fg, accent)

    # ── Beverages ──
    if cat == 'Beverages':
        if 'coffee' in name:
            color = '#78350F' if 'dark' in name else '#92400E' if 'espresso' in name else '#A16207' if 'light' in name else '#7C2D12'
            if 'whole bean' in name:
                return icon_bag('#FEF3C7', color, '#D97706')
            return icon_bag('#FEF3C7', color, '#92400E')
        if 'tea' in name:
            tea_colors = {
                'black': '#78350F', 'earl grey': '#6B21A8', 'green': '#166534',
                'chamomile': '#CA8A04', 'peppermint': '#047857', 'chai': '#92400E'
            }
            tc = '#78350F'
            for k, v in tea_colors.items():
                if k in name:
                    tc = v
                    break
            return icon_box('#FEF3C7', tc, '#D97706')
        if 'soda' in name or 'cola' in name or 'root beer' in name or 'ginger ale' in name or 'lemon-lime' in name:
            soda_c = '#DC2626' if 'cola' in name else '#16A34A' if 'lemon' in name or 'ginger' in name else '#EA580C' if 'orange' in name else '#7C3AED' if 'root' in name else '#3B82F6'
            return icon_can(bg, soda_c, '#E5E7EB')
        if 'sparkling' in name:
            return icon_bottle(bg, '#93C5FD', '#BFDBFE')
        if 'water' in name:
            return icon_bottle(bg, '#93C5FD', '#3B82F6')
        if 'juice' in name or 'orange juice' in name or 'apple juice' in name or 'cranberry' in name:
            jc = '#F97316' if 'orange' in name else '#DC2626' if 'cranberry' in name or 'tomato' in name else '#22C55E' if 'apple' in name else '#7C3AED' if 'grape' in name else '#EAB308' if 'pineapple' in name else '#F59E0B'
            return icon_box(bg, jc, '#FBBF24')
        if 'energy' in name:
            return icon_can(bg, '#10B981', '#FDE68A')
        if 'sports' in name:
            return icon_bottle(bg, '#3B82F6', '#93C5FD')
        return icon_bottle(bg, fg, accent)

    # ── Breakfast ──
    if cat == 'Breakfast':
        if 'cereal' in name:
            return icon_cereal(bg, fg, accent)
        if 'oatmeal' in name or 'oat' in name:
            return icon_oatmeal(bg, fg, accent)
        if 'waffle' in name:
            return icon_waffle(bg, fg, accent)
        if 'pancake' in name:
            if 'mix' in name:
                return icon_box(bg, fg, accent)
            return icon_pancake(bg, fg, accent)
        if 'toaster pastri' in name or 'toaster strudel' in name:
            return icon_toaster_pastry(bg, fg, accent)
        if 'breakfast sandwich' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _path('M12 28 Q12 16 32 12 Q52 16 52 28 Z', fg) +
                _rect(14,28,36,6,'#FBBF24',0) +
                _rect(14,34,36,4,'#EF4444',0) +
                _path('M12 38 L52 38 Q52 50 32 52 Q12 50 12 38 Z', fg)
            )
        if 'breakfast burrito' in name:
            return icon_burrito(bg, fg, accent)
        if 'breakfast bar' in name:
            return icon_granola_bar(bg, fg, accent)
        return icon_box(bg, fg, accent)

    # ── Canned Goods ──
    if cat == 'Canned Goods':
        if 'bean' in name:
            bean_c = '#1F2937' if 'black' in name else '#F5F5F4' if 'cannellini' in name or 'great northern' in name else '#991B1B' if 'kidney' in name else '#D4A054' if 'pinto' in name or 'chickpea' in name else '#6B7280'
            return icon_can(bg, '#6B7280', bean_c)
        if 'corn' in name:
            return icon_can(bg, '#6B7280', '#EAB308')
        if 'pea' in name:
            return icon_can(bg, '#6B7280', '#16A34A')
        if 'green bean' in name:
            return icon_can(bg, '#6B7280', '#15803D')
        if 'tomato' in name:
            return icon_can(bg, '#6B7280', '#DC2626')
        if 'soup' in name:
            return icon_can(bg, '#6B7280', '#F97316' if 'tomato' in name else '#FBBF24' if 'chicken' in name else '#D4A054' if 'chowder' in name else '#9CA3AF')
        if 'tuna' in name or 'salmon' in name or 'chicken' in name:
            return icon_can(bg, '#6B7280', '#F472B6' if 'salmon' in name else '#FBBF24')
        if 'coconut' in name:
            return icon_can(bg, '#6B7280', '#F5F5F4')
        if 'fruit' in name:
            return icon_can(bg, '#6B7280', '#F97316' if 'peach' in name or 'mandarin' in name else '#EAB308')
        return icon_can(bg, fg, accent)

    # ── Condiments ──
    if cat == 'Condiments':
        if 'ketchup' in name:
            return icon_bottle('#FEE2E2', '#DC2626', '#991B1B')
        if 'mustard' in name:
            return icon_bottle('#FEF3C7', '#CA8A04', '#92400E')
        if 'mayo' in name:
            return icon_jar('#F5F5F4', '#F3F4F6', '#D4D4D8')
        if 'hot sauce' in name or 'sriracha' in name:
            return icon_bottle('#FEE2E2', '#EF4444', '#991B1B')
        if 'bbq' in name:
            return icon_bottle('#FEE2E2', '#92400E', '#DC2626')
        if 'soy sauce' in name or 'worcestershire' in name or 'teriyaki' in name:
            return icon_bottle('#1F2937', '#374151', '#6B7280')
        if 'olive' in name:
            return icon_olive(bg, '#15803D' if 'green' in name else '#581C87', accent)
        if 'pickle' in name or 'relish' in name:
            return icon_jar(bg, '#16A34A', '#15803D')
        if 'pesto' in name:
            return icon_jar(bg, '#15803D', '#166534')
        if 'salsa' in name:
            return icon_jar(bg, '#DC2626', '#991B1B')
        if 'alfredo' in name:
            return icon_jar(bg, '#F5F5F4', '#D4D4D8')
        if 'marinara' in name:
            return icon_jar(bg, '#DC2626', '#EF4444')
        if 'salad dressing' in name:
            return icon_dressing_bottle(bg, fg, accent)
        if 'caper' in name:
            return icon_jar(bg, '#15803D', '#16A34A')
        return icon_bottle(bg, fg, accent)

    # ── Dairy ──
    if cat == 'Dairy':
        if 'milk' in name:
            if 'chocolate' in name:
                return icon_milk('#FEF3C7', '#78350F', '#D97706')
            if 'buttermilk' in name:
                return icon_milk('#FFFBEB', '#FDE68A', '#F59E0B')
            return icon_milk(bg, '#BFDBFE', '#2563EB')
        if 'butter' in name or 'margarine' in name:
            return icon_butter(bg, '#FBBF24', '#F59E0B')
        if 'egg' in name:
            return icon_egg(bg, '#FDE68A', '#FBBF24')
        if 'yogurt' in name:
            color_map = {
                'blueberry': '#6366F1', 'strawberry': '#EC4899', 'cherry': '#DC2626',
                'peach': '#F97316', 'raspberry': '#E11D48', 'honey': '#D97706',
                'vanilla': '#FDE68A', 'plain': '#E5E7EB'
            }
            yc = '#93C5FD'
            for k, v in color_map.items():
                if k in name:
                    yc = v
                    break
            return icon_yogurt(bg, '#F3F4F6', yc)
        if 'cheese' in name:
            if 'cream cheese' in name:
                return icon_box(bg, '#F3F4F6', '#D4D4D8')
            if 'cottage' in name or 'ricotta' in name:
                return icon_yogurt(bg, '#F3F4F6', '#E5E7EB')
            if 'feta' in name or 'goat' in name:
                return icon_cheese(bg, '#F5F5F4', '#D4D4D8')
            if 'parmesan' in name:
                return icon_can(bg, '#FBBF24', '#F59E0B')
            if 'string' in name:
                return svg_wrap(
                    _rect(0,0,64,64,bg,8) +
                    _rect(26,8,12,48,fg,4) +
                    _rect(26,8,12,8,accent,2) +
                    _line(30,20,30,50,accent,1) +
                    _line(34,20,34,50,accent,1)
                )
            return icon_cheese(bg, '#FBBF24', '#F59E0B')
        if 'cream' in name or 'whipping' in name:
            return icon_bottle(bg, '#F3F4F6', '#E5E7EB')
        if 'sour cream' in name:
            return icon_yogurt(bg, '#F3F4F6', '#E5E7EB')
        return icon_milk(bg, fg, accent)

    # ── Deli ──
    if cat == 'Deli':
        if 'ham' in name or 'turkey' in name or 'roast beef' in name or 'salami' in name:
            mc = '#F472B6' if 'ham' in name else '#D4A054' if 'turkey' in name else '#991B1B' if 'roast beef' in name or 'salami' in name else fg
            return icon_deli_meat(bg, mc, '#EC4899')
        if 'hummus' in name:
            return icon_hummus(bg, fg, accent)
        if 'coleslaw' in name or 'salad' in name:
            return icon_salad(bg, '#4ADE80' if 'coleslaw' in name else '#F59E0B', accent)
        return icon_deli_meat(bg, fg, accent)

    # ── Frozen ──
    if cat == 'Frozen':
        if 'pizza' in name:
            return icon_pizza(bg, '#FBBF24', '#DC2626' if 'pepperoni' in name else '#F5F5F4')
        if 'ice cream' in name:
            return icon_ice_cream(bg, '#78350F' if 'chocolate' in name else '#FDE68A', '#D97706')
        if 'frozen yogurt' in name:
            return icon_ice_cream(bg, '#F9A8D4', '#EC4899')
        if 'ice pop' in name:
            return icon_popsicle(bg, '#EF4444', '#3B82F6')
        if 'french fries' in name:
            return icon_fries(bg, '#DC2626', '#FBBF24')
        if 'nugget' in name:
            return icon_nuggets(bg, '#FBBF24', '#D97706')
        if 'fish stick' in name:
            return icon_fries(bg, '#D97706', '#FBBF24')
        if 'meatball' in name:
            return icon_meatball(bg, '#991B1B', '#DC2626')
        if 'shrimp' in name:
            return icon_shrimp(bg, '#F472B6', '#EC4899')
        if 'burrito' in name:
            return icon_burrito(bg, '#FBBF24', '#DC2626')
        if 'pie crust' in name:
            return icon_pie(bg, '#FBBF24', '#D97706')
        if 'puff pastry' in name:
            return icon_box(bg, '#FBBF24', '#D97706')
        if 'stir-fry' in name:
            return icon_stir_fry(bg, '#4ADE80', '#15803D')
        if 'berries' in name or 'strawberr' in name or 'blueberr' in name:
            return icon_berry(bg, '#DC2626' if 'strawberr' in name else '#4338CA' if 'blueberr' in name else '#7C3AED', '#EC4899')
        if 'broccoli' in name:
            return icon_leafy(bg, '#16A34A', '#15803D')
        if 'cauliflower' in name:
            return icon_leafy(bg, '#F5F5F4', '#D4D4D8')
        if 'corn' in name:
            return icon_bag(bg, '#EAB308', '#CA8A04')
        if 'peas' in name:
            return icon_peas(bg, '#16A34A', '#15803D')
        if 'spinach' in name:
            return icon_leafy(bg, '#166534', '#15803D')
        if 'mixed veg' in name:
            return icon_bag(bg, '#4ADE80', '#F59E0B')
        return icon_bag(bg, fg, accent)

    # ── Grains & Pasta ──
    if cat == 'Grains & Pasta':
        if 'rice' in name:
            return icon_rice_bag(bg, fg, '#92400E' if 'brown' in name else '#FDE68A' if 'jasmine' in name or 'basmati' in name else accent)
        if 'couscous' in name or 'quinoa' in name:
            return icon_rice_bag(bg, fg, '#FDE68A' if 'couscous' in name else '#DC2626')
        if 'oat' in name:
            return icon_bag(bg, fg, accent)
        if 'pasta' in name:
            # Vary color by type
            pc = fg
            if 'gluten-free' in name:
                pc = '#F59E0B'
            elif 'protein' in name:
                pc = '#DC2626'
            elif 'whole wheat' in name:
                pc = '#92400E'
            return icon_noodles(bg, pc, accent)
        return icon_bag(bg, fg, accent)

    # ── Household ──
    if cat == 'Household':
        if 'cleaner' in name or 'all-purpose' in name or 'disinfect' in name or 'bleach' in name:
            return icon_spray(bg, fg, accent)
        if 'dish soap' in name:
            return icon_bottle(bg, '#3B82F6', '#93C5FD')
        if 'dishwasher' in name:
            if 'pod' in name:
                return icon_box(bg, '#3B82F6', '#93C5FD')
            return icon_bottle(bg, '#60A5FA', '#93C5FD')
        if 'laundry' in name:
            return icon_bottle(bg, '#8B5CF6', '#A78BFA')
        if 'fabric softener' in name:
            return icon_bottle(bg, '#EC4899', '#F9A8D4')
        if 'dryer sheet' in name:
            return icon_box(bg, '#A78BFA', '#8B5CF6')
        if 'paper towel' in name:
            return icon_roll(bg, fg, accent)
        if 'toilet paper' in name:
            return icon_roll(bg, '#F5F5F4', '#D4D4D8')
        if 'tissue' in name:
            return icon_box(bg, '#93C5FD', '#BFDBFE')
        if 'trash bag' in name:
            return icon_bag('#374151', '#1F2937', '#4B5563')
        if 'freezer bag' in name or 'sandwich bag' in name:
            return icon_bag(bg, '#60A5FA', '#93C5FD')
        if 'sponge' in name:
            return icon_sponge(bg, '#FBBF24', '#16A34A')
        if 'scrub brush' in name:
            return icon_sponge(bg, '#34D399', '#065F46')
        if 'battery' in name:
            return icon_battery(bg, '#374151', '#FBBF24')
        if 'candle' in name:
            return icon_candle(bg, '#FDE68A', '#F59E0B')
        if 'hand sanitizer' in name:
            return icon_bottle(bg, '#93C5FD', '#3B82F6')
        if 'hydrogen peroxide' in name or 'rubbing alcohol' in name:
            return icon_bottle(bg, '#92400E', '#D4D4D8')
        if 'air freshener' in name:
            return icon_spray(bg, '#A78BFA', '#8B5CF6')
        if 'cough syrup' in name:
            return icon_bottle(bg, '#DC2626', '#991B1B')
        return icon_spray(bg, fg, accent)

    # ── International ──
    if cat == 'International':
        if 'noodle' in name or 'ramen' in name or 'soba' in name:
            return icon_noodle_pack(bg, fg, accent)
        if 'curry' in name:
            return icon_curry(bg, '#DC2626' if 'red' in name else '#16A34A', accent)
        if 'taco' in name:
            return icon_taco(bg, '#FBBF24', '#D97706')
        if 'tortilla' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _circle(32,34,20,'#16A34A') +
                _circle(32,34,14,'#22C55E')
            )
        if 'pita chip' in name:
            return icon_chips(bg, fg, accent)
        if 'salsa verde' in name:
            return icon_jar(bg, '#16A34A', '#15803D')
        if 'enchilada' in name:
            return icon_can(bg, '#DC2626', '#991B1B')
        if 'hummus' in name:
            return icon_hummus(bg, fg, accent)
        if 'sriracha' in name:
            return icon_bottle(bg, '#DC2626', '#991B1B')
        if 'gochujang' in name:
            return icon_jar(bg, '#DC2626', '#7F1D1D')
        if 'fish sauce' in name:
            return icon_bottle(bg, '#92400E', '#D97706')
        if 'coconut aminos' in name:
            return icon_bottle(bg, '#92400E', '#78350F')
        return icon_jar(bg, fg, accent)

    # ── Kitchen Supplies ──
    if cat == 'Kitchen Supplies':
        if 'foil' in name:
            return icon_foil_roll(bg, '#9CA3AF', '#D4D4D8')
        if 'plastic wrap' in name or 'parchment' in name:
            return icon_foil_roll(bg, '#E5E7EB' if 'plastic' in name else '#FDE68A', accent)
        if 'glove' in name:
            return icon_gloves(bg, '#FBBF24', '#F59E0B')
        if 'cup' in name:
            return icon_cup(bg, fg, accent)
        if 'plate' in name:
            return icon_plate(bg, fg, accent)
        if 'napkin' in name:
            return icon_napkin(bg, fg, accent)
        if 'cutlery' in name:
            return icon_cutlery(bg, fg, accent)
        return icon_box(bg, fg, accent)

    # ── Meat & Seafood ──
    if cat == 'Meat & Seafood':
        if 'bacon' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _path('M12 16 Q22 24 22 32 Q22 40 12 48', '#991B1B') +
                _path('M22 16 Q32 24 32 32 Q32 40 22 48', '#F87171') +
                _path('M32 16 Q42 24 42 32 Q42 40 32 48', '#991B1B') +
                _path('M42 16 Q52 24 52 32 Q52 40 42 48', '#F87171')
            )
        if 'salmon' in name:
            return icon_fish(bg, '#F472B6', '#EC4899')
        if 'cod' in name or 'tilapia' in name:
            return icon_fish(bg, '#F5F5F4', '#D4D4D8')
        if 'shrimp' in name:
            return icon_shrimp(bg, '#F472B6', '#EC4899')
        if 'hot dog' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _rect(14,26,36,12,'#DC2626',6) +
                _path('M14 26 Q32 16 50 26', '#FBBF24') +
                _path('M16 34 Q32 44 48 34', '#FBBF24')
            )
        if 'sausage' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _rect(10,26,44,12,fg,6) +
                _line(22,26,22,38,accent,1) +
                _line(32,26,32,38,accent,1) +
                _line(42,26,42,38,accent,1)
            )
        if 'chicken' in name:
            return icon_meat_cut(bg, '#FBBF24', '#F59E0B')
        if 'steak' in name or 'chuck' in name or 'stew' in name or 'ground beef' in name:
            return icon_meat_cut(bg, '#DC2626', '#991B1B')
        if 'pork' in name or 'lamb' in name:
            return icon_meat_cut(bg, '#F472B6', '#EC4899')
        if 'turkey' in name:
            return icon_meat_cut(bg, '#D4A054', '#92400E')
        return icon_meat_cut(bg, fg, accent)

    # ── Pantry ──
    if cat == 'Pantry':
        if 'oil' in name:
            color = '#15803D' if 'olive' in name else '#16A34A' if 'avocado' in name else '#FBBF24'
            return icon_oil_bottle(bg, color, accent)
        if 'vinegar' in name:
            return icon_vinegar(bg, '#92400E' if 'apple cider' in name or 'balsamic' in name else '#D4D4D8', accent)
        if 'honey' in name:
            return icon_honey_jar(bg, '#FBBF24', '#F59E0B')
        if 'maple syrup' in name or 'corn syrup' in name or 'molasses' in name:
            return icon_syrup(bg, '#92400E' if 'molasses' in name else '#D97706', accent)
        if 'peanut butter' in name:
            return icon_peanut_butter(bg, '#D97706', '#92400E')
        if 'jam' in name or 'jelly' in name:
            return icon_jam_jar(bg, '#7C3AED' if 'grape' in name else '#DC2626', accent)
        if 'broth' in name:
            return icon_broth_box(bg, '#FBBF24' if 'chicken' in name else '#991B1B' if 'beef' in name else '#16A34A', accent)
        if 'extract' in name:
            return icon_extract_bottle(bg, '#92400E' if 'vanilla' in name else '#D97706', accent)
        return icon_bottle(bg, fg, accent)

    # ── Personal Care ──
    if cat == 'Personal Care':
        if 'toothpaste' in name:
            return icon_tube(bg, '#3B82F6', '#93C5FD')
        if 'toothbrush' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _rect(30,8,4,40,fg,2) +
                _rect(22,8,20,10,accent,4) +
                _line(26,12,26,18,bg,1) +
                _line(32,12,32,18,bg,1) +
                _line(38,12,38,18,bg,1)
            )
        if 'shampoo' in name or 'conditioner' in name:
            return icon_bottle(bg, '#A78BFA' if 'shampoo' in name else '#EC4899', accent)
        if 'body wash' in name:
            return icon_bottle(bg, '#3B82F6', accent)
        if 'soap' in name:
            return icon_soap(bg, '#A78BFA', '#8B5CF6')
        if 'lotion' in name or 'hand cream' in name or 'sunscreen' in name:
            return icon_tube(bg, '#FBBF24' if 'sunscreen' in name else fg, accent)
        if 'deodorant' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _rect(22,10,20,44,fg,6) +
                _rect(22,10,20,10,accent,4) +
                _ellipse(32,54,10,4,accent)
            )
        if 'razor' in name:
            return icon_razor(bg, fg, accent)
        if 'shaving cream' in name:
            return icon_can(bg, '#6B7280', '#E5E7EB')
        if 'dental floss' in name:
            return icon_box(bg, '#3B82F6', '#93C5FD')
        if 'mouthwash' in name:
            return icon_bottle(bg, '#16A34A', '#4ADE80')
        if 'cotton ball' in name or 'cotton swab' in name:
            return icon_box(bg, '#F3F4F6', '#BFDBFE')
        if 'facial cleanser' in name:
            return icon_tube(bg, '#93C5FD', '#3B82F6')
        if 'hair gel' in name or 'hair spray' in name:
            return icon_bottle(bg, '#6D28D9', accent) if 'gel' in name else icon_spray(bg, fg, accent)
        if 'lip balm' in name:
            return icon_tube(bg, '#EC4899', '#F9A8D4')
        return icon_bottle(bg, fg, accent)

    # ── Pet ──
    if cat == 'Pet':
        if 'cat' in name:
            if 'litter' in name:
                return icon_bag('#DCFCE7', '#6B7280', '#4ADE80')
            return icon_cat('#DCFCE7', '#F59E0B' if 'dry' in name else '#F472B6', '#15803D')
        if 'dog' in name:
            if 'shampoo' in name:
                return icon_bottle('#DCFCE7', '#4ADE80', '#15803D')
            if 'treat' in name:
                return svg_wrap(
                    _rect(0,0,64,64,'#DCFCE7',8) +
                    _path('M32 12 Q42 12 44 24 L48 32 L44 32 Q42 40 36 42 L36 52 L28 52 L28 42 Q22 40 20 32 L16 32 L20 24 Q22 12 32 12 Z', '#D97706') +
                    _circle(28,26,2,'#15803D') + _circle(36,26,2,'#15803D')
                )
            return icon_dog('#DCFCE7', '#D97706' if 'dry' in name else '#F472B6', '#15803D')
        if 'waste' in name:
            return icon_waste_bag('#DCFCE7', '#4ADE80', '#15803D')
        return icon_pet_bowl(bg, fg, accent)

    # ── Pharmacy ──
    if cat == 'Pharmacy':
        if 'bandage' in name or 'tape' in name:
            return icon_bandage(bg, '#F5F5F4', '#DC2626')
        if 'thermometer' in name:
            return icon_thermometer(bg, '#E5E7EB', '#DC2626')
        if 'vitamin' in name or 'multivitamin' in name:
            return icon_vitamin(bg, '#F59E0B', '#EAB308')
        if 'cold medicine' in name or 'cough' in name:
            return icon_bottle(bg, '#DC2626', '#991B1B')
        return icon_pill(bg, fg, accent)

    # ── Produce ──
    if cat == 'Produce':
        if 'apple' in name:
            c = '#DC2626' if 'fuji' in name or 'gala' in name else '#16A34A' if 'granny' in name else '#F59E0B'
            return icon_round_fruit(bg, c, '#15803D')
        if 'banana' in name:
            return icon_banana(bg, '#FBBF24', '#92400E')
        if 'orange' in name or 'clementine' in name:
            return icon_citrus(bg, '#F97316', '#FDBA74')
        if 'lemon' in name:
            return icon_citrus(bg, '#FDE047', '#EAB308')
        if 'lime' in name:
            return icon_citrus(bg, '#4ADE80', '#16A34A')
        if 'grapefruit' in name:
            return icon_citrus(bg, '#FB923C', '#F472B6')
        if 'grape' in name:
            return icon_grape(bg, '#7C3AED' if 'red' in name else '#4ADE80', '#6D28D9' if 'red' in name else '#15803D')
        if 'blueberr' in name:
            return icon_berry(bg, '#4338CA', '#6366F1')
        if 'blackberr' in name:
            return icon_berry(bg, '#1F2937', '#374151')
        if 'strawberr' in name:
            return icon_berry(bg, '#DC2626', '#EF4444')
        if 'cherr' in name:
            return icon_berry(bg, '#991B1B', '#DC2626')
        if 'cranberr' in name:
            return icon_berry(bg, '#DC2626', '#991B1B')
        if 'cantaloupe' in name:
            return icon_melon(bg, '#D97706', '#FBBF24')
        if 'coconut' in name:
            return icon_coconut(bg, '#92400E', '#F5F5F4')
        if 'mango' in name:
            return icon_mango(bg, '#F97316', '#FBBF24')
        if 'kiwi' in name:
            return icon_kiwi(bg, '#92400E', '#4ADE80')
        if 'avocado' in name:
            return icon_avocado(bg, '#15803D', '#FBBF24')
        if 'pear' in name:
            # This won't match "bell pepper" since we check pepper first below... actually let's be careful
            pass
        if 'carrot' in name:
            return icon_long_veggie(bg, '#F97316', '#16A34A')
        if 'celery' in name:
            return icon_long_veggie(bg, '#4ADE80', '#16A34A')
        if 'asparagus' in name:
            return icon_asparagus(bg, '#16A34A', '#15803D')
        if 'broccoli' in name:
            return icon_leafy(bg, '#16A34A', '#15803D')
        if 'cauliflower' in name:
            return icon_leafy(bg, '#F5F5F4', '#D4D4D8')
        if 'brussels' in name:
            return icon_leafy(bg, '#22C55E', '#16A34A')
        if 'spinach' in name or 'arugula' in name or 'kale' in name:
            return icon_leafy(bg, '#166534', '#15803D')
        if 'lettuce' in name or 'cabbage' in name:
            c = '#991B1B' if 'red' in name else '#4ADE80'
            return icon_leafy(bg, c, '#16A34A')
        if 'pepper' in name or 'jalape' in name:
            pc = '#16A34A' if 'green' in name or 'jalape' in name else '#DC2626' if 'red' in name else '#EAB308'
            return icon_pepper(bg, pc, '#15803D')
        if 'tomato' in name:
            return icon_tomato(bg, '#DC2626', '#15803D')
        if 'cucumber' in name:
            return icon_long_veggie(bg, '#16A34A', '#15803D')
        if 'corn' in name:
            return icon_corn(bg, '#FBBF24', '#16A34A')
        if 'eggplant' in name:
            return icon_eggplant(bg, '#7C3AED', '#6D28D9')
        if 'mushroom' in name:
            return icon_mushroom(bg, '#D4A054', '#92400E')
        if 'onion' in name or 'leek' in name:
            return icon_onion(bg, '#FBBF24' if 'onion' in name else '#16A34A', '#92400E')
        if 'potato' in name:
            return icon_potato(bg, '#D4A054', '#92400E')
        if 'beet' in name:
            return icon_round_fruit(bg, '#991B1B', '#7F1D1D')
        if 'green bean' in name:
            return icon_peas(bg, '#16A34A', '#15803D')
        if 'garlic' in name:
            return svg_wrap(
                _rect(0,0,64,64,bg,8) +
                _path('M32 10 Q48 18 48 36 Q48 54 32 56 Q16 54 16 36 Q16 18 32 10 Z', '#F5F5F4') +
                _path('M32 10 L32 16', '#D4D4D8') +
                _line(24,24,24,48,'#E5E7EB',1) +
                _line(32,20,32,48,'#E5E7EB',1) +
                _line(40,24,40,48,'#E5E7EB',1)
            )
        if 'ginger' in name:
            return icon_ginger(bg, '#D4A054', '#92400E')
        if 'basil' in name or 'cilantro' in name or 'mint' in name or 'dill' in name or 'herb' in name or 'chive' in name or 'oregano' in name or 'sage' in name:
            return icon_herb_sprig(bg, '#16A34A', '#15803D')
        # Remaining produce: pear-shaped fallback or round
        return icon_round_fruit(bg, fg, accent)

    # ── Fallback ──
    return icon_box(bg, fg, accent)


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    with open(TEMPLATE_PATH) as f:
        products = json.load(f)

    count = 0
    for product in products:
        slug = product['imageSlug']
        svg = get_icon(product)
        path = os.path.join(OUTPUT_DIR, f'{slug}.svg')
        with open(path, 'w', newline='\n') as f:
            f.write(svg)
        count += 1

    print(f'Generated {count} SVG icons in {OUTPUT_DIR}')


if __name__ == '__main__':
    main()
