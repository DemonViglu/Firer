"""Author a new modular snow traveler. Does not read or overwrite the old character.

Blender coordinates: meters, Z up, -Y forward. Bone rotations only; no scale keys.
Each garment follows the same explicitly weighted limb chains as its body region.
Run: blender --background --python Authoring/TravelerRebuild/build_traveler.py
"""
from pathlib import Path
from math import sin, cos, pi, radians
import bpy
import bmesh
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).resolve().parent
OUT = ROOT / 'Assets/FirePlay/Art/TravelerRebuild'
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW = SOURCE / 'Preview'
PREVIEW.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.fps = 30

def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

BODY = collection('01_Body')
CLOTH = collection('02_Clothing')
HAIR = collection('03_Hair')
DETAIL = collection('04_Face_and_Accessories')
RIG = collection('00_Skeleton')
STUDIO = collection('90_Studio_not_exported')
export_objects = []

def material(name, color, roughness=.7, metallic=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return m

skin = material('Traveler_Skin', (.65, .36, .25), .62)
coat = material('Traveler_PineWool', (.105, .23, .22), .88)
lining = material('Traveler_IvoryFleece', (.78, .72, .57), .95)
trousers = material('Traveler_CharcoalTwill', (.105, .13, .17), .9)
leather = material('Traveler_OxbloodLeather', (.19, .065, .04), .63)
sole = material('Traveler_Rubber', (.035, .043, .049), .92)
scarfmat = material('Traveler_OchreKnit', (.85, .39, .085), .94)
hairmat = material('Traveler_Chestnut', (.095, .033, .022), .72)
eye_white = material('Traveler_EyeIvory', (.78, .79, .73), .34)
iris = material('Traveler_Iris', (.12, .24, .22), .35)
dark = material('Traveler_Pupil', (.009, .015, .019), .4)
lip = material('Traveler_Lip', (.38, .13, .11), .7)
brass = material('Traveler_Brass', (.55, .3, .1), .38, .65)

armature = bpy.data.armatures.new('Traveler_Skeleton')
rig = bpy.data.objects.new('Traveler_Rig', armature)
RIG.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name, head, tail, parent=None, connected=False):
    b = armature.edit_bones.new(name)
    b.head, b.tail = head, tail
    if parent:
        b.parent = armature.edit_bones[parent]
        b.use_connect = connected
    b.roll = 0
    return b

bone('root', (0,0,0), (0,0,.15))
bone('hips', (0,0,.89), (0,0,1.01), 'root')
bone('spine', (0,0,1.01), (0,0,1.16), 'hips', True)
bone('chest', (0,0,1.16), (0,0,1.35), 'spine', True)
bone('neck', (0,0,1.35), (0,0,1.47), 'chest', True)
bone('head', (0,0,1.47), (0,0,1.69), 'neck', True)
for sign, side in [(1,'L'),(-1,'R')]:
    bone('clavicle.'+side, (0,0,1.34), (sign*.17,0,1.34), 'chest')
    bone('upper_arm.'+side, (sign*.17,0,1.34), (sign*.425,0,1.34), 'clavicle.'+side, True)
    bone('forearm.'+side, (sign*.425,0,1.34), (sign*.65,0,1.34), 'upper_arm.'+side, True)
    bone('hand.'+side, (sign*.65,0,1.34), (sign*.73,0,1.34), 'forearm.'+side, True)
    for j, finger in enumerate(['index','middle','ring','little']):
        yy = -.029 + j*.02
        length = [.078,.086,.079,.061][j]
        x0 = .725
        parent = 'hand.'+side
        for k in range(3):
            name = f'{finger}.{k+1:02d}.{side}'
            bone(name, (sign*(x0+k*length/3), yy, 1.338),
                 (sign*(x0+(k+1)*length/3), yy, 1.338), parent, k>0)
            parent=name
    bone('thumb.01.'+side, (sign*.674,-.025,1.335), (sign*.696,-.053,1.329), 'hand.'+side)
    bone('thumb.02.'+side, (sign*.696,-.053,1.329), (sign*.724,-.073,1.326), 'thumb.01.'+side, True)
    bone('thumb.03.'+side, (sign*.724,-.073,1.326), (sign*.745,-.081,1.325), 'thumb.02.'+side, True)
    bone('thigh.'+side, (sign*.093,0,.92), (sign*.102,-.018,.505), 'hips')
    bone('shin.'+side, (sign*.102,-.018,.505), (sign*.105,0,.12), 'thigh.'+side, True)
    bone('foot.'+side, (sign*.105,0,.12), (sign*.105,-.13,.06), 'shin.'+side, True)
    bone('toes.'+side, (sign*.105,-.13,.06), (sign*.105,-.2,.055), 'foot.'+side, True)
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True
armature.display_type='OCTAHEDRAL'

def clamp(x): return max(0,min(1,x))
def smooth(x):
    t=clamp(x)
    return t*t*(3-2*t)
def blend(a,b,t):
    t=smooth(t)
    return {a:1-t,b:t}
def torso_weights(z):
    if z<1.04: return blend('hips','spine',(z-.95)/.09)
    if z<1.2: return blend('spine','chest',(z-1.07)/.13)
    return {'chest':1}
def arm_weights(x, side):
    x=abs(x)
    if x<.24: return blend('clavicle.'+side,'upper_arm.'+side,(x-.155)/.085)
    if x<.485: return blend('upper_arm.'+side,'forearm.'+side,(x-.378)/.095)
    return blend('forearm.'+side,'hand.'+side,(x-.607)/.065)
def leg_weights(z, side):
    if z>.81: return blend('thigh.'+side,'hips',(z-.81)/.15)
    if z>.42: return blend('shin.'+side,'thigh.'+side,(z-.435)/.14)
    if z<.20: return blend('foot.'+side,'shin.'+side,(z-.12)/.08)
    return {'shin.'+side:1}

def mesh(name, verts, faces, weights, mat, coll, subdiv=1):
    data=bpy.data.meshes.new(name+'_Mesh')
    data.from_pydata(verts,[],faces)
    data.update()
    bm=bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    obj=bpy.data.objects.new(name,data)
    coll.objects.link(obj)
    data.materials.append(mat)
    for f in data.polygons: f.use_smooth=True
    groups={}
    for i, ww in enumerate(weights):
        total=sum(ww.values())
        for name_,w in ww.items():
            if w<=.00001: continue
            g=groups.get(name_)
            if g is None: g=obj.vertex_groups.new(name=name_); groups[name_]=g
            g.add([i],w/total,'REPLACE')
    obj.parent=rig
    modifier=obj.modifiers.new('Deform_Same_Skeleton','ARMATURE')
    modifier.object=rig
    modifier.use_deform_preserve_volume=True
    if subdiv:
        sub=obj.modifiers.new('Surface_Refinement','SUBSURF')
        sub.levels=sub.render_levels=subdiv
    export_objects.append(obj)
    return obj

def loft(name, rings, mat, coll, wf, axis='z', n=20, subdiv=1):
    # Rings: axial coordinate, two elliptical radii, two cross-axis offsets.
    vs=[]; fs=[]; ws=[]
    for value,r1,r2,c1,c2 in rings:
        for j in range(n):
            a=2*pi*j/n
            cross1=c1+r1*cos(a); cross2=c2+r2*sin(a)
            v=(cross1,cross2,value) if axis=='z' else (value,cross1,cross2)
            vs.append(v); ws.append(wf(v))
    for row in range(len(rings)-1):
        for j in range(n):
            k=row*n+j; k1=row*n+(j+1)%n
            fs.append((k,k1,k1+n,k+n))
    fs.append(tuple(reversed(range(n))))
    fs.append(tuple((len(rings)-1)*n+j for j in range(n)))
    obj=mesh(name,vs,fs,ws,mat,coll,subdiv)
    uv=obj.data.uv_layers.new(name='UVMap')
    for poly in obj.data.polygons:
        indices=[obj.data.loops[i].vertex_index for i in poly.loop_indices]
        seam=any(i%n==0 for i in indices) and any(i%n==n-1 for i in indices)
        for loop_index in poly.loop_indices:
            vi=obj.data.loops[loop_index].vertex_index
            u=(vi%n)/n
            if seam and vi%n==0: u=1
            uv.data[loop_index].uv=(u,(vi//n)/(len(rings)-1))
    return obj

def oval(name,center,scale,mat,coll,binding='head',segments=20):
    # A shaped closed loft, also used for small facial features and accessories.
    rings=[]
    for j in range(1,12):
        t=-pi/2+pi*j/12
        rings.append((center[2]+scale[2]*sin(t),scale[0]*cos(t),scale[1]*cos(t),center[0],center[1]))
    return loft(name,rings,mat,coll,lambda v:{binding:1},n=segments)

torso=[(.865,.103,.073,0,0),(.91,.132,.083,0,0),(.975,.118,.081,0,0),
       (1.035,.098,.072,0,0),(1.09,.10,.075,0,-.002),(1.16,.124,.086,0,-.008),
       (1.225,.143,.083,0,0),(1.29,.157,.072,0,0),(1.335,.164,.061,0,0),
       (1.365,.138,.05,0,0),(1.395,.052,.043,0,0),(1.455,.045,.04,0,0)]
loft('Body_Torso',torso,skin,BODY,lambda v:torso_weights(v[2]),n=24)

# Jaw, cheeks, temples, brow and cranium are authored as distinct contour rings.
headrings=[(1.445,.032,.032,0,-.006),(1.462,.047,.047,0,-.009),
    (1.485,.066,.057,0,-.007),(1.515,.081,.065,0,-.005),
    (1.55,.09,.073,0,.001),(1.585,.091,.074,0,.001),
    (1.62,.087,.076,0,.005),(1.652,.073,.065,0,.009),
    (1.674,.05,.047,0,.01),(1.681,.021,.023,0,.01)]
loft('Body_Head',headrings,skin,BODY,lambda v:{'head':1},n=32,subdiv=2)
for sign,side in [(1,'L'),(-1,'R')]:
    oval('Ear_'+side,(sign*.09,.005,1.558),(.018,.014,.033),skin,BODY)
    oval('EyeWhite_'+side,(sign*.038,-.067,1.573),(.020,.006,.0085),eye_white,DETAIL)
    oval('Iris_'+side,(sign*.037,-.072,1.573),(.007,.002,.0075),iris,DETAIL)
    oval('Pupil_'+side,(sign*.037,-.0738,1.573),(.0035,.0015,.0048),dark,DETAIL)
    oval('Catchlight_'+side,(sign*.035,-.075,1.576),(.0015,.001,.0015),eye_white,DETAIL)
    brow=oval('Brow_'+side,(sign*.039,-.071,1.598),(.025,.006,.004),hairmat,DETAIL)
oval('NoseBridge',(0,-.069,1.553),(.009,.012,.022),skin,BODY)
oval('NoseTip',(0,-.080,1.539),(.012,.012,.008),skin,BODY)
oval('UpperLip',(0,-.063,1.511),(.022,.005,.0032),lip,DETAIL)
oval('LowerLip',(0,-.064,1.505),(.02,.0045,.004),lip,DETAIL)

# Hair cap is a swept shell with a varying hairline; separate front locks frame the face.
vs=[]; fs=[]
N=40
for row in range(12):
    t=row/11
    for j in range(N):
        a=2*pi*j/N
        front=max(0,-sin(a))
        edge=1.90-.63*front
        polar=.07+(edge-.07)*t
        vs.append((.099*sin(polar)*cos(a),.01+.087*sin(polar)*sin(a),1.589+.106*cos(polar)))
for i in range(11):
    for j in range(N):
        fs.append((i*N+j,i*N+(j+1)%N,(i+1)*N+(j+1)%N,(i+1)*N+j))
fs.append(tuple(reversed(range(N))))
hair=mesh('Hair_Cap',vs,fs,[{'head':1} for v in vs],hairmat,HAIR,2)
solid=hair.modifiers.new('Hair_Thickness','SOLIDIFY'); solid.thickness=.006
for sign,side in [(1,'L'),(-1,'R')]:
    loft('Hair_FramingLock_'+side,[(1.493,.009,.012,sign*.083,-.037),
         (1.53,.017,.016,sign*.09,-.033),(1.58,.019,.023,sign*.087,-.04),
         (1.625,.018,.024,sign*.065,-.066),(1.66,.017,.021,sign*.037,-.061)],
         hairmat,HAIR,lambda v:{'head':1},n=16)
oval('Hair_BackKnot',(0,.103,1.575),(.047,.04,.05),hairmat,HAIR)

for sign,side in [(1,'L'),(-1,'R')]:
    # Densely spaced loops at elbow/knee make smooth two-bone weight transitions.
    ar=[(.115,.050,.055),(.145,.055,.060),(.18,.055,.053),(.21,.053,.05),(.27,.047,.045),(.34,.037,.037),
        (.38,.032,.032),(.405,.029,.03),(.425,.028,.029),(.445,.029,.03),
        (.47,.031,.032),(.52,.031,.032),(.58,.025,.025),(.63,.019,.02),(.654,.018,.019)]
    loft('Body_Arm_'+side,[(sign*x,ry,rz,0,1.34) for x,ry,rz in ar],skin,BODY,
         lambda v,s=side:arm_weights(v[0],s),axis='x')
    loft('Body_Palm_'+side,[(sign*x,ry,rz,0,1.339) for x,ry,rz in
         [(.642,.019,.018),(.665,.027,.019),(.69,.036,.018),(.716,.036,.015),(.735,.03,.012)]],
         skin,BODY,lambda v,s=side:{'hand.'+s:1},axis='x',n=16)
    for j,finger in enumerate(['index','middle','ring','little']):
        length=[.078,.086,.079,.061][j]; yy=-.029+j*.02
        rings=[]
        for k in range(10):
            t=k/9
            rad=.009*(1-.48*t)
            if k==9: rad=.0025
            rings.append((sign*(.722+length*t),rad,rad,yy,1.338))
        def fw(v,s=side,f=finger,length=length):
            t=clamp((abs(v[0])-.722)/length)*3
            if t<.8: return {f+'.01.'+s:1}
            if t<1.3: return blend(f+'.01.'+s,f+'.02.'+s,(t-.8)/.5)
            return blend(f+'.02.'+s,f+'.03.'+s,(t-1.8)/.5)
        loft('Body_'+finger+'_'+side,rings,skin,BODY,fw,axis='x',n=12)
    loft('Body_Thumb_'+side,[(sign*x,r,r,y,z) for x,y,z,r in
         [(.672,-.027,1.335,.014),(.688,-.043,1.332,.012),(.705,-.061,1.328,.01),
          (.724,-.073,1.326,.009),(.743,-.08,1.325,.004)]],skin,BODY,
         lambda v,s=side:blend('thumb.01.'+s,'thumb.03.'+s,(abs(v[0])-.69)/.05),axis='x',n=12)
    leg=[(.115,.028,.032),(.18,.036,.039),(.26,.045,.048),(.35,.046,.053),
         (.43,.039,.043),(.475,.038,.041),(.505,.039,.044),(.535,.042,.045),
         (.57,.048,.052),(.67,.061,.062),(.77,.069,.07),(.86,.072,.073),(.945,.069,.07)]
    loft('Body_Leg_'+side,[(z,rx,ry,sign*.1,-.01 if .42<z<.56 else 0) for z,rx,ry in leg],
         skin,BODY,lambda v,s=side:leg_weights(v[2],s))
    loft('Trousers_Leg_'+side,[(z,rx+.007,ry+.009,sign*.1,-.01 if .42<z<.56 else 0) for z,rx,ry in leg if z>.17],
         trousers,CLOTH,lambda v,s=side:leg_weights(v[2],s))
    loft('Coat_Sleeve_'+side,[(sign*x,ry+.012,rz+.013,0,1.34) for x,ry,rz in ar if x<.64],
         coat,CLOTH,lambda v,s=side:arm_weights(v[0],s),axis='x')
    loft('Fleece_Cuff_'+side,[(sign*x,.034,.034,0,1.34) for x in [.591,.596,.628,.633]],
         lining,CLOTH,lambda v,s=side:arm_weights(v[0],s),axis='x')
    loft('Boot_'+side,[(.045,.053,.118,sign*.105,-.064),(.06,.057,.128,sign*.105,-.065),
         (.10,.052,.106,sign*.105,-.056),(.135,.042,.067,sign*.105,-.018),
         (.18,.044,.048,sign*.105,0),(.215,.047,.05,sign*.105,0),(.225,.047,.05,sign*.105,0)],
         leather,CLOTH,lambda v,s=side:leg_weights(v[2],s))
    loft('Boot_Sole_'+side,[(z,.059,.13,sign*.105,-.066) for z in [.025,.03,.048,.053]],
         sole,CLOTH,lambda v,s=side:{'foot.'+s:1})
    loft('Boot_Cuff_'+side,[(z,.05,.054,sign*.105,0) for z in [.207,.21,.229,.232]],
         lining,CLOTH,lambda v,s=side:leg_weights(v[2],s))
    for z in [.12,.146,.169,.19]:
        oval('BootLace_'+side+str(z),(sign*.105,-.055,z),(.032,.006,.004),lining,DETAIL,'foot.'+side)

loft('Trousers_Waist',[(.86,.13,.09,0,0),(.90,.137,.092,0,0),(.97,.129,.09,0,0),(.985,.124,.088,0,0)],
     trousers,CLOTH,lambda v:torso_weights(v[2]))
loft('Coat_Body',[(.90,.15,.101,0,0),(.912,.153,.102,0,0),(.99,.134,.099,0,0),
     (1.075,.124,.096,0,0),(1.16,.14,.105,0,-.003),(1.25,.163,.096,0,0),
     (1.32,.178,.078,0,0),(1.365,.143,.067,0,0),(1.39,.062,.051,0,0)],
     coat,CLOTH,lambda v:torso_weights(v[2]),n=28)
loft('Coat_Hem',[(z,.151,.103,0,0) for z in [.897,.902,.923,.928]],
     lining,CLOTH,lambda v:torso_weights(v[2]),n=28)
for z in [.977,1.035,1.096,1.159,1.221,1.28]:
    oval('Coat_Button_'+str(z),(.018,-.105,z),(.009,.005,.009),brass,DETAIL,
         'hips' if z<1 else 'spine' if z<1.15 else 'chest')
for sign,side in [(1,'L'),(-1,'R')]:
    oval('Pocket_'+side,(sign*.079,-.091,1.009),(.041,.009,.05),coat,CLOTH,'spine')
loft('Scarf_Collar',[(1.362,.073,.063,0,-.003),(1.371,.08,.07,0,-.006),
     (1.394,.079,.072,0,-.007),(1.42,.068,.062,0,-.008),(1.425,.063,.058,0,-.008)],
     scarfmat,CLOTH,lambda v:{'chest':1},n=28)
loft('Scarf_FrontTail',[(1.095,.019,.007,-.044,-.111),(1.11,.025,.01,-.043,-.115),
     (1.22,.032,.011,-.044,-.116),(1.31,.029,.012,-.032,-.098),(1.383,.033,.013,-.018,-.079)],
     scarfmat,CLOTH,lambda v:torso_weights(v[2]),n=16)

# Small travel pack and bedroll are separate, rigidly chest-bound accessories.
oval('Backpack',(0,.127,1.195),(.116,.058,.148),leather,DETAIL,'chest')
loft('Bedroll',[(x,.049,.049,.142,1.369) for x in [-.124,-.12,.12,.124]],
     lining,DETAIL,lambda v:{'chest':1},axis='x',n=20)

# Actions are sampled into quaternion channels on the shared deformation rig.
# Smooth rotations, phase offsets, fingers, and weight shifts replace global scaling.
def world_rotation(name, x=0,y=0,z=0):
    q = Quaternion(Vector((1,0,0)),radians(x)) @ Quaternion(Vector((0,1,0)),radians(y)) @ Quaternion(Vector((0,0,1)),radians(z))
    basis=armature.bones[name].matrix_local.to_quaternion()
    return basis.inverted() @ q @ basis

def make_action(name, frames, pose):
    rig.animation_data_create()
    action=bpy.data.actions.new(name)
    action.use_fake_user=True
    rig.animation_data.action=action
    for frame in range(1,frames+1,2):
        t=(frame-1)/(frames-1)
        values, hip = pose(t)
        for pb in rig.pose.bones:
            pb.rotation_mode='QUATERNION'
            pb.rotation_quaternion=world_rotation(pb.name,*values.get(pb.name,(0,0,0)))
            pb.location=(0,0,0)
            pb.scale=(1,1,1)
            pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
        # hips is vertical in rest space: local Y is world Z.
        rig.pose.bones['hips'].location=(0,hip,0)
        rig.pose.bones['hips'].keyframe_insert('location',frame=frame,group='hips')
    values,hip=pose(1)
    for pb in rig.pose.bones:
        pb.rotation_quaternion=world_rotation(pb.name,*values.get(pb.name,(0,0,0)))
        pb.keyframe_insert('rotation_quaternion',frame=frames,group=pb.name)
    rig.pose.bones['hips'].location=(0,hip,0)
    rig.pose.bones['hips'].keyframe_insert('location',frame=frames,group='hips')
    track=rig.animation_data.nla_tracks.new(); track.name=name
    strip=track.strips.new(name,1,action)
    strip.action_frame_start=1; strip.action_frame_end=frames
    track.mute=True
    return action

def neutral(t):
    v={'upper_arm.L':(0,76,0),'upper_arm.R':(0,-76,0),
       'forearm.L':(0,0,-8),'forearm.R':(0,0,8),
       'chest':(1.2*sin(t*2*pi),0,0),'head':(0,0,.8*sin(t*2*pi))}
    for s in ['L','R']:
        for f in ['index','middle','ring','little']:
            for k in [1,2,3]: v[f'{f}.{k:02d}.{s}']=(0,0,-12 if s=='L' else 12)
    return v,.003*sin(t*2*pi)
def walk(t,run=False):
    v,h=neutral(t); p=2*pi*t
    amp=31 if run else 22
    for sign,s in [(1,'L'),(-1,'R')]:
        phase=p+(0 if sign==1 else pi)
        v['thigh.'+s]=(-amp*sin(phase),0,0)
        v['shin.'+s]=(max(0,sin(phase-.55))*(63 if run else 39),0,0)
        v['foot.'+s]=(-max(0,sin(phase-.3))*14,0,0)
        v['upper_arm.'+s]=(amp*.65*sin(phase),sign*76,0)
        v['forearm.'+s]=(0,0,-sign*(32 if run else 12))
    v['hips']=(5 if run else 0,0,2*sin(p))
    v['chest']=(2,0,-3*sin(p))
    return v,.012*(1-cos(2*p))
def wave(t):
    v,h=neutral(t); a=smooth(min(t/.2,(1-t)/.2))
    v['upper_arm.R']=(0,-76+116*a,0)
    v['forearm.R']=(0,0,(-55+12*sin(t*8*pi))*a)
    v['hand.R']=(0,15*sin(t*8*pi)*a,0)
    v['head']=(0,0,-5*a)
    return v,h
def thanks(t):
    v,h=neutral(t); a=sin(pi*t)**2
    v['spine']=(13*a,0,0);v['chest']=(9*a,0,0);v['head']=(12*a,0,0)
    return v,-.012*a
def sitting(t):
    v,h=neutral(t)
    for s in ['L','R']:
        v['thigh.'+s]=(-85,0,0);v['shin.'+s]=(88,0,0)
    v['upper_arm.L']=(-20,55,0);v['upper_arm.R']=(-20,-55,0)
    v['forearm.L']=(0,0,-25);v['forearm.R']=(0,0,25)
    return v,-.37+.002*sin(t*2*pi)
def jump(t):
    v,h=neutral(t); a=sin(pi*t)
    for s in ['L','R']:
        v['thigh.'+s]=(-28*a,0,0);v['shin.'+s]=(45*a,0,0)
    v['upper_arm.L']=(-30*a,76-25*a,0);v['upper_arm.R']=(-30*a,-76+25*a,0)
    return v,-.035*sin(t*2*pi)**2

def activity(t, kind):
    v,h=neutral(t); breath=sin(t*2*pi)
    if kind=='Stargaze':
        v,h=sitting(t);v['head']=(-19+breath,0,0);return v,h
    if kind=='Marshmallow':
        v['upper_arm.R']=(-37,-48,0);v['forearm.R']=(0,0,35+2*breath)
        v['hand.R']=(0,3*breath,0);v['head']=(7,0,-6)
    elif kind=='Fishing':
        v['upper_arm.R']=(-29,-52,0);v['upper_arm.L']=(-26,54,0)
        v['forearm.R']=(0,0,52+2*breath);v['forearm.L']=(0,0,-55)
        v['chest']=(3+breath,0,0)
    else:
        v['upper_arm.L']=(-17,45,-12);v['upper_arm.R']=(-12,-53,9)
        v['forearm.L']=(0,0,-64);v['forearm.R']=(0,0,74+7*breath)
        v['hand.R']=(0,0,5*breath);v['head']=(7,0,4)
    return v,h

actions={}
for name,frames,pose in [('Idle',91,neutral),('Walk',31,walk),('Run',25,lambda t:walk(t,True)),
    ('Jump',37,jump),('Wave',61,wave),('Thanks',61,thanks),('Rest',91,sitting)]:
    actions[name]=make_action(name,frames,pose)
for kind in ['Marshmallow','Fishing','Guitar','Stargaze']:
    actions[kind]=make_action(kind,61,lambda t,k=kind:activity(t,k))

rig.animation_data.action=None
for pb in rig.pose.bones:
    pb.rotation_quaternion=(1,0,0,0); pb.location=(0,0,0);pb.scale=(1,1,1)

# Export with the rest skeleton and all named NLA clips; do not export studio.
bpy.ops.object.select_all(action='DESELECT')
for obj in export_objects+[rig]: obj.select_set(True)
bpy.context.view_layer.objects.active=rig
for track in rig.animation_data.nla_tracks: track.mute=False
bpy.ops.export_scene.fbx(filepath=str(OUT/'Traveler_Modular.fbx'),use_selection=True,
    object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',
    add_leaf_bones=False,use_armature_deform_only=True,mesh_smooth_type='FACE',
    bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,
    bake_anim_simplify_factor=0,apply_unit_scale=True)
for track in rig.animation_data.nla_tracks: track.mute=True
rig.animation_data.action=actions['Idle']
scene.frame_set(1)

# A lit studio portrait is an authoring preview, not gameplay acceptance.
def move_to(obj,coll):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    coll.objects.link(obj)
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object; floor.name='StudioGround'; move_to(floor,STUDIO)
floor.data.materials.append(material('Studio_Slate',(.12,.16,.18)))
def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,location,power,size,color in [
    ('Key',(-3,-4,5),600,4,(1,.82,.64)),('Fill',(3,-2,3),380,3,(.62,.79,1)),
    ('Rim',(1,3,4),850,3,(1,.66,.32))]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);STUDIO.objects.link(obj);obj.location=location;aim(obj,(0,0,1))
camera_data=bpy.data.cameras.new('PortraitCamera'); camera=bpy.data.objects.new('PortraitCamera',camera_data)
STUDIO.objects.link(camera);camera.location=(2,-5,2.5);aim(camera,(0,0,.9));camera_data.type='ORTHO';camera_data.ortho_scale=2.15
scene.camera=camera
scene.world=bpy.data.worlds.new('StudioWorld');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.23,.29,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
for obj in bpy.context.selected_objects: obj.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Traveler_Modular.blend'))
scene.render.filepath=str(PREVIEW/'Traveler_Portrait.png')
bpy.ops.render.render(write_still=True)
print('TRAVELER_AUTHORED',str(OUT/'Traveler_Modular.fbx'))
