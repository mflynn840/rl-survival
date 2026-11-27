"""Author the game's editable Blender source and export lightweight runtime meshes.

Run with Blender 4.5+: blender -b --python tools/generate_models.py
The forms and colors follow Assets/Art/player.png, enemy.png, guns.jpg and blocks.png.
"""
from pathlib import Path
import math
import bpy

ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / "Assets" / "Resources" / "Models"
SOURCE = ROOT / "Assets" / "Models"
for folder in (UNITY, SOURCE):
    folder.mkdir(parents=True, exist_ok=True)
ART=ROOT / "Assets" / "Art"
RUNTIME_ART=ROOT / "Assets" / "Resources" / "Art"
RUNTIME_ART.mkdir(parents=True,exist_ok=True)

atlas=bpy.data.images.load(str(ART / "blocks.png"),check_existing=True)
atlas_pixels=list(atlas.pixels)
def export_tile(name,row,column):
    width,height=134,119
    x0,y0=113+column*143,atlas.size[1]-(8+row*125+height)
    pixels=[]
    for y in range(y0,y0+height):
        start=(y*atlas.size[0]+x0)*4
        pixels.extend(atlas_pixels[start:start+width*4])
    image=bpy.data.images.new(name,width=width,height=height,alpha=True)
    image.pixels.foreach_set(pixels)
    image.filepath_raw=str(RUNTIME_ART/(name+".png"));image.file_format="PNG";image.save()
    bpy.data.images.remove(image)

for name,row,column in [
    ("GroundDirt",0,0),("GroundGrass",0,3),("RoadAsphalt",5,7),
    ("MotelPlaster",2,9),("MotelStone",1,9),("MotelTimber",3,2),
    ("BlockScrap",3,1),("BlockBrick",2,0),("BlockSteel",4,3),("BlockTitanium",4,7),
    ("BlockShock",6,4),("BlockConcrete",5,2),("BlockRoof",3,9),("BlockSpikes",4,4),
]: export_tile(name,row,column)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

def mat(name, color, metal=0, rough=.75):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    p = material.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    return material

M = {
    "skin": mat("warm skin", (.49,.31,.24)), "hair": mat("dark hair", (.075,.052,.042)),
    "jacket": mat("weathered brown leather", (.15,.105,.077)), "hoodie": mat("charcoal hoodie", (.115,.135,.15)),
    "denim": mat("blue denim", (.08,.13,.18)), "boot": mat("worn boot", (.09,.065,.05)),
    "suit": mat("black wool suit", (.06,.065,.07)), "shirt": mat("cream shirt", (.8,.79,.72)),
    "tie": mat("burgundy tie", (.26,.045,.04)), "metal": mat("gunmetal", (.11,.12,.13), .78, .36),
    "grip": mat("grip polymer", (.045,.05,.052), .1, .8), "steel": mat("brushed steel", (.38,.39,.4), .8, .3),
    "wood": mat("scuffed timber", (.29,.17,.09), .05), "brick": mat("old brick", (.43,.19,.12)),
    "concrete": mat("raw concrete", (.39,.38,.34)), "titanium": mat("blue titanium", (.3,.4,.44), .78),
    "shock": mat("shock turquoise", (.04,.58,.53), .48), "blood": mat("blood red", (.35,.018,.012)),
}

def empty(name, parent=None, at=(0,0,0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = at
    return obj

def cube(name, at, size, material, parent=None, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if bevel:
        mod = obj.modifiers.new("edge wear", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        obj.modifiers.new("weighted normals", "WEIGHTED_NORMAL")
    return obj

def sphere(name, at, radius, material, parent=None, scale=(1,1,1), segments=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=10, radius=radius)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.scale = scale
    obj.data.materials.append(material)
    for face in obj.data.polygons: face.use_smooth = True
    return obj

def cylinder(name, at, radius, depth, material, parent=None, vertices=12, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.rotation_euler = rot
    obj.data.materials.append(material)
    return obj

def cone(name, at, radius, depth, material, parent=None, vertices=8):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=0, depth=depth)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.data.materials.append(material)
    return obj

def wedge(name, parent, material):
    verts = [(-.49,-.49,-.49),(.49,-.49,-.49),(.49,-.49,.49),(-.49,-.49,.49),(0,.49,-.49),(0,.49,.49)]
    faces = [(0,1,2,3),(0,4,1),(3,2,5),(0,3,5,4),(1,4,5,2)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    mod = obj.modifiers.new("soft stone edges", "BEVEL")
    mod.width=.012
    mod.segments=1
    obj.modifiers.new("face normals", "WEIGHTED_NORMAL")
    return obj

def character(name, raider):
    root = empty(name)
    body = empty("Body", root)
    jacket = M["suit"] if raider else M["jacket"]
    pants = M["suit"] if raider else M["denim"]
    sphere("torso tailored jacket", (0,1.22,0), .42, jacket, body, (.86,1,.53), 28)
    sphere("waist", (0,.79,-.005), .29, jacket, body, (.93,.68,.58), 24)
    sphere("neck", (0,1.72,.02), .13, M["skin"], body, (.8,1,.8))
    sphere("face", (0,1.91,.045), .24, M["skin"], body, (.84,1.09,.81), 24)
    sphere("jaw", (0,1.78,.125), .16, M["skin"], body, (1,.56,.83), 20)
    sphere("hair crown", (0,2.06,-.015), .245, M["hair"], body, (.9,.48,.9), 20)
    for x in (-.115,.115):
        sphere("eye", (x,1.94,.215), .018, M["grip"], body)
        sphere("eyebrow", (x,1.99,.214), .034, M["hair"], body, (1.3,.18,.4))
    sphere("nose", (0,1.86,.23), .045, M["skin"], body, (.55,.75,1.2))
    if raider:
        cube("shirt chest", (0,1.31,.202), (.25,.55,.035), M["shirt"], body, .012)
        cube("tie", (0,1.25,.235), (.07,.42,.025), M["tie"], body)
        for side in (-1,1):
            lapel = cube("suit lapel", (side*.17,1.46,.211), (.21,.42,.04), jacket, body, .01)
            lapel.rotation_euler[1] = side*.2
        cube("belt buckle", (0,.76,.19), (.1,.065,.02), M["steel"], body)
    else:
        sphere("hood", (0,1.62,-.17), .29, M["hoodie"], body, (1.15,.72,.68))
        cube("hoodie", (0,1.21,.2), (.45,.61,.045), M["hoodie"], body, .022)
        cube("jacket zipper", (0,1.17,.235), (.018,.62,.025), M["steel"], body)
        for side in (-1,1): cube("jacket pocket", (side*.2,.98,.225), (.19,.1,.022), jacket, body, .01)
    for side, label in ((-1,"Left"),(1,"Right")):
        arm = empty(f"{label}Arm", body, (side*.39,1.54,0))
        sphere(f"{label} sleeve", (side*.075,-.25,.11 if not raider else 0), .145, jacket, arm, (.86,1.72,.88), 20)
        sphere(f"{label} fist", (side*.08,-.59,.27 if not raider else 0), .105, M["skin"], arm, (.82,1,.82), 16)
        leg = empty(f"{label}Leg", body, (side*.17,.72,0))
        sphere(f"{label} trouser", (0,-.29,0), .15, pants, leg, (.78,2.12,.84), 20)
        cube(f"{label} boot", (0,-.63,.12), (.245,.18,.43), M["boot"], leg, .035)
    if raider:
        weapon = empty("MeleeMount", body, (.5,.98,.32))
        cylinder("knife handle", (0,0,0), .04, .23, M["wood"], weapon, rot=(math.pi/2,0,0))
        cube("knife blade", (0,0,.23), (.07,.025,.31), M["steel"], weapon, .012)
    else:
        empty("GunMount", body, (0,1.27,.46))
    return root

def gun(name, length, color, stock=True, optic=False, drum=False, launcher=False):
    root=empty(name)
    upper=M["steel"] if color=="steel" else M["metal"]
    cube("receiver", (0,0,.1), (.19,.18,.45), upper, root, .022)
    cube("handguard", (0,.012,.42), (.145,.145,max(.2,length*.52)), upper, root, .025)
    cylinder("barrel", (0,.015,.4+length*.43), .043 if not launcher else .1, max(.2,length*.8), M["grip"] if launcher else M["steel"], root, rot=(math.pi/2,0,0))
    cylinder("muzzle", (0,.015,.43+length*.85), .063 if not launcher else .13, .12, M["metal"], root, rot=(math.pi/2,0,0))
    cube("pistol grip", (0,-.17,-.055), (.1,.32,.13), M["grip"], root, .017)
    cube("trigger guard", (0,-.13,.12), (.09,.04,.15), M["steel"], root, .012)
    if stock: cube("shoulder stock", (0,-.045,-.35), (.14,.15,.46), M["grip"], root, .023)
    cube("magazine", (0,-.23,.25), (.12,.31,.15) if not drum else (.2,.27,.25), M["grip"], root, .015)
    if optic:
        cube("sight rail", (0,.123,.05), (.07,.025,.35), M["grip"], root, .005)
        cylinder("optic", (0,.21,.06), .075, .22, M["metal"], root, rot=(math.pi/2,0,0))
    for z in (.2,.4,.6):
        if z<length: cube("vent", (0,.086,z), (.115,.014,.032), M["grip"], root, .003)
    empty("Muzzle", root, (0,.015,.49+length*.85))
    return root

def block(name, material, kind):
    root=empty(name)
    if kind=="roof":
        wedge("triangular roof slope", root, material)
        for z in (-.42,.42): cube("roof edge", (0,-.46,z), (.98,.08,.08), M["steel"], root, .01)
    elif kind=="spikes":
        cube("spike plate", (0,-.4,0), (.96,.16,.96), M["metal"], root, .025)
        for x in (-.27,0,.27):
            for z in (-.27,0,.27): cone("hardened spike", (x,-.08,z), .11, .63, M["steel"], root)
    else:
        cube("solid wall", (0,0,0), (.96,.98,.96), material, root, .025 if kind in ("scrap","brick") else .012)
        if kind=="brick":
            for y in (-.32,0,.32):
                for x in (-.31,.31): cube("brick course", (x,y,.488), (.55,.23,.012), M["brick"], root, .005)
        if kind=="shock":
            for y in (-.29,.29): cube("powered rail", (0,y,.495), (.84,.055,.025), M["shock"], root, .01)
        if kind in ("steel","titanium","concrete"):
            for x in (-.4,.4):
                for y in (-.4,.4): cylinder("plate bolt", (x,y,.5), .025, .02, M["grip"], root, rot=(math.pi/2,0,0))
    return root

roots=[character("Survivor", False),character("Raider", True)]
roots += [
    gun("Carbine",.55,"metal",optic=True), gun("Shotgun",.83,"steel"),
    gun("Railgun",1.1,"steel",optic=True), gun("SMG",.35,"metal",stock=False),
    gun("GrenadeLauncher",.82,"metal",launcher=True), gun("LMG",.95,"metal",optic=True,drum=True),
]
roots += [block(n,m,k) for n,m,k in [
    ("Scrap",M["wood"],"scrap"),("Brick",M["concrete"],"brick"),
    ("Steel",M["steel"],"steel"),("Titanium",M["titanium"],"titanium"),
    ("ShockGrid",M["metal"],"shock"),("ReinforcedConcrete",M["concrete"],"concrete"),
    ("Roof",M["wood"],"roof"),("Spikes",M["metal"],"spikes"),
]]

def children(root):
    todo=[root]
    while todo:
        obj=todo.pop()
        yield obj
        todo.extend(obj.children)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "RLSurvivalAssets.blend"))
for root in roots:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in children(root): obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(UNITY / (root.name+".fbx")), use_selection=True, add_leaf_bones=False, axis_forward="-Z", axis_up="Y")
print("Exported", len(roots), "model pairs")
