using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rebuildable, original miniature coastal district. All distances are metres.
public static class PalmHarborBuilder
{
    const string Root = "Assets/PalmHarbor";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly List<Light> accentLights = new List<Light>();
    static Transform world;
    static Font font;
    static int meshIndex;
    static System.Random rng;
    static Color C(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
    static float R(float a, float b) => Mathf.Lerp(a,b,(float)rng.NextDouble());

    [MenuItem("Palm Harbor/Build Coastal District")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode before rebuilding.");
        foreach(var folder in new[]{"Materials","Textures","Meshes","Scenes"}) System.IO.Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(previous.isDirty) EditorSceneManager.SaveScene(previous);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        rng=new System.Random(1986); meshIndex=0; mats.Clear(); accentLights.Clear();
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        world=new GameObject("PALM HARBOR | Original Coastal District").transform;
        Palette();
        Landscape();
        Architecture();
        Streetscape();
        Beach();
        LightingAndPlayer();
        CombineGeometry();
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/PalmHarbor.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/PalmHarbor.unity",true)};
        AssetDatabase.SaveAssets();
        var sv=SceneView.lastActiveSceneView;
        if(sv!=null){sv.LookAt(new Vector3(-8,8,0),Quaternion.Euler(19,-39,0),92);sv.sceneViewState.showFog=true;sv.sceneViewState.showImageEffects=true;}
        Debug.Log("PALM HARBOR built and saved. Press Play to explore. WASD + mouse / E radio / N evening / F tour.");
    }

    static Material Mat(string name,string hex,float smooth=.3f,float metal=0,float glow=0)
    {
        string path=Root+"/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.color=C(hex);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);
        if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",C(hex)*glow);}
        mats[name]=m;return m;
    }
    static void Palette()
    {
        Mat("Ivory stucco","#F3DEC5");Mat("Coral stucco","#E99D9C");Mat("Mint stucco","#98C7BD");
        Mat("Lilac stucco","#B6AAC8");Mat("Peach stucco","#E7B69D");Mat("Seafoam trim","#4FA9AB");
        Mat("Stone","#C5B9AC");Mat("White trim","#F8EADB");Mat("Midnight metal","#263D4C",.65f,.55f);
        Mat("Bronze","#A07D56",.65f,.65f);Mat("Glass teal","#234D60",.94f,.6f);Mat("Warm window","#ECB77E",.6f,0,.35f);
        Mat("Dark interior","#233442");Mat("Neon pink","#FF559C",.4f,0,3.2f);Mat("Neon aqua","#69F5E4",.4f,0,2.6f);
        Mat("Neon amber","#FFD192",.4f,0,2f);Mat("Road paint","#E9D7A8");Mat("White paint","#EEE4D5");
        Mat("Asphalt","#3D4754",.2f);Mat("Sidewalk","#C5B8AC",.24f);Mat("Sand","#E6CCA0",.1f);
        Mat("Wet sand","#BCA887",.68f);Mat("Grass","#667C63",.1f);Mat("Palm bark","#8A6D52",.15f);
        Mat("Palm dark","#294D44",.25f);Mat("Palm leaf","#47775C",.27f);Mat("Wood","#986D50",.25f);
        Mat("Red fabric","#C86F78",.15f);Mat("Aqua fabric","#74B9BD",.2f);Mat("Tyre","#22262E",.1f);
        Mat("Car coral","#D8737C",.83f,.45f);Mat("Car cream","#E6D6AF",.8f,.4f);Mat("Car turquoise","#3B9B9D",.85f,.45f);
        Mat("Car purple","#626180",.8f,.4f);Mat("Tail light","#E94F64",.7f,0,.8f);Mat("Headlamp","#FFE8B0",.7f,0,.7f);
        Texture("Asphalt",256,34f,.13f);Texture("Sidewalk",128,8f,.035f);Texture("Sand",256,18f,.08f);
        Texture("Ivory stucco",128,4f,.025f);Texture("Coral stucco",128,4f,.025f);Texture("Mint stucco",128,4f,.025f);
        Texture("Wood",128,5f,.09f);
    }
    static void Texture(string material,int size,float repeat,float strength)
    {
        var tex=new Texture2D(size,size,TextureFormat.RGBA32,true);tex.name=material+" grain";tex.wrapMode=TextureWrapMode.Repeat;
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float n=((float)rng.NextDouble()-.5f)*strength+Mathf.PerlinNoise(x*.13f,y*.13f)*strength*.45f;
            float v=.91f+n;pixels[y*size+x]=new Color(v,v,v,1);
        }
        tex.SetPixels(pixels);tex.Apply();string p=Root+"/Textures/"+material+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        if(existing){EditorUtility.CopySerialized(tex,existing);UnityEngine.Object.DestroyImmediate(tex);tex=existing;}
        else AssetDatabase.CreateAsset(tex,p);
        mats[material].mainTexture=tex;mats[material].mainTextureScale=Vector2.one*repeat;
    }
    static Transform Group(string name,Transform parent=null)
    {
        var t=new GameObject(name).transform;t.SetParent(parent?parent:world,false);return t;
    }
    static GameObject Shape(string name,PrimitiveType type,Vector3 pos,Vector3 scale,string mat,Transform parent,bool solid=false)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;
        g.GetComponent<Renderer>().sharedMaterial=mats[mat];if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;
    }
    static GameObject Box(string n,Vector3 p,Vector3 s,string m,Transform t,bool solid=false)=>Shape(n,PrimitiveType.Cube,p,s,m,t,solid);
    static GameObject Cylinder(string n,Vector3 p,float radius,float height,string m,Transform t,bool solid=false)=>Shape(n,PrimitiveType.Cylinder,p,new Vector3(radius*2,height*.5f,radius*2),m,t,solid);
    static void Beam(string n,Vector3 a,Vector3 b,float radius,string m,Transform t)
    {
        var g=Cylinder(n,(a+b)*.5f,radius,Vector3.Distance(a,b),m,t);g.transform.up=(b-a).normalized;
    }
    static void Text(string value,Vector3 p,float size,string color,Transform t,float yaw=-90,FontStyle style=FontStyle.Normal)
    {
        var g=new GameObject("Sign | "+value);g.transform.SetParent(t,false);g.transform.localPosition=p;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
        var text=g.AddComponent<TextMesh>();text.font=font;text.text=value;text.fontSize=96;text.characterSize=size/10;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=C(color);text.fontStyle=style;
        g.GetComponent<MeshRenderer>().sharedMaterial=font.material;
    }

    static void Landscape()
    {
        var t=Group("01 | Boulevard and terrain");
        Box("Coastal foundation",new Vector3(-14.5f,-1,-2),new Vector3(181,2,250),"Sand",t,true);
        Box("Ocean Drive asphalt",new Vector3(-.5f,.035f,0),new Vector3(19,.1f,214),"Asphalt",t,true);
        Box("West pavement",new Vector3(-14.5f,.19f,0),new Vector3(9,.38f,214),"Sidewalk",t,true);
        Box("Promenade",new Vector3(18,.18f,0),new Vector3(17,.36f,214),"Sidewalk",t,true);
        Box("Cycle lane",new Vector3(12,.386f,0),new Vector3(2,.025f,214),"Seafoam trim",t);
        for(int z=-103;z<=103;z+=4)
        {
            Box("Centre dash",new Vector3(0,.099f,z),new Vector3(.12f,.015f,1.9f),"Road paint",t);
            Box("Pavement expansion",new Vector3(18,.369f,z),new Vector3(17,.008f,.028f),"Stone",t);
            Box("Hotel pavement expansion",new Vector3(-14.5f,.389f,z),new Vector3(9,.008f,.028f),"Stone",t);
            Box("Beach edge paver",new Vector3(26.7f,.26f,z),new Vector3(.5f,.52f,3.95f),"White trim",t,true);
        }
        foreach(float x in new[]{-9.8f,9.1f})Box("Solid lane edge",new Vector3(x,.101f,0),new Vector3(.1f,.01f,214),"White paint",t);
        for(int z=-98;z<=98;z+=9)
        {
            Box("Parking bay end",new Vector3(-7.1f,.102f,z),new Vector3(4.6f,.011f,.1f),"White paint",t);
            Box("Parking bay side",new Vector3(-4.8f,.102f,z+4.5f),new Vector3(.1f,.011f,8.9f),"White paint",t);
        }
        foreach(int z in new[]{-64,31,94})
        {
            for(int i=0;i<10;i++)Box("Zebra crossing",new Vector3(-8.5f+i*1.8f,.11f,z),new Vector3(.85f,.02f,3.1f),"White paint",t);
            Box("Stop line",new Vector3(-2,.11f,z-3),new Vector3(14,.02f,.22f),"White paint",t);
        }
        Box("Beach sand",new Vector3(53,-.11f,0),new Vector3(52,.3f,234),"Sand",t,true);
        foreach(float end in new[]{-102f,102f})
        {
            Box("Neighbourhood cross street",new Vector3(-31,.04f,end),new Vector3(82,.1f,10),"Asphalt",t,true);
            for(int j=0;j<8;j++)Box("Cross-street lane marking",new Vector3(-65+j*9,.105f,end),new Vector3(3,.01f,.1f),"Road paint",t);
        }
        Box("Wet tidal sand",new Vector3(76,-.1f,0),new Vector3(15,.14f,250),"Wet sand",t,true);
        var waterMat=new Material(Shader.Find("PalmHarbor/Ocean")??Shader.Find("Universal Render Pipeline/Lit"));
        waterMat.name="Atlantic water";waterMat.SetColor("_DeepColor",C("#296D86"));waterMat.SetColor("_ShallowColor",C("#65B9AE"));waterMat.SetColor("_FoamColor",C("#DBE9D6"));
        waterMat=SaveAsset(waterMat,Root+"/Materials/Atlantic water.mat");
        var sea=new GameObject("Atlantic | animated coastal water");sea.transform.SetParent(t,false);sea.AddComponent<MeshFilter>().sharedMesh=OceanMesh();sea.AddComponent<MeshRenderer>().sharedMaterial=waterMat;
        // Invisible boundaries keep an assessment walk inside the finished environment.
        foreach(var b in new[]{new Vector3(-72,8,0),new Vector3(79,8,0),new Vector3(0,8,-108),new Vector3(0,8,108)})
        {
            var g=new GameObject("District boundary");g.transform.SetParent(t,false);g.transform.localPosition=b;g.AddComponent<BoxCollider>().size=Mathf.Abs(b.z)>100?new Vector3(180,18,1):new Vector3(1,18,220);
        }
    }
    static Mesh OceanMesh()
    {
        const int nx=75,nz=100;var v=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[v.Length];var tri=new int[nx*nz*6];int k=0;
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){int i=z*(nx+1)+x;v[i]=new Vector3(76+x*7,-.035f,-500+z*10);uv[i]=new Vector2(x/(float)nx,z/(float)nz);}
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=z*(nx+1)+x;tri[k++]=i;tri[k++]=i+nx+1;tri[k++]=i+1;tri[k++]=i+1;tri[k++]=i+nx+1;tri[k++]=i+nx+2;}
        var m=new Mesh{name="Coastal water grid"};m.vertices=v;m.uv=uv;m.triangles=tri;m.RecalculateNormals();return SaveAsset(m,Root+"/Meshes/Ocean.asset");
    }

    static void Architecture()
    {
        Hotel("THE AVALON",-37,-65,29,27,20,"Coral stucco","Neon pink",0);
        Hotel("THE MARLIN",-37,-29,29,29,16,"Mint stucco","Neon aqua",1);
        Diner();
        Hotel("COLONY",-38,38,31,31,24,"Ivory stucco","Neon aqua",2);
        Hotel("CASA FLORA",-38,77,31,30,18,"Lilac stucco","Neon pink",3);
        var backdrop=Group("03 | Rear neighbourhood skyline");
        for(int i=0;i<9;i++)
        {
            float z=-100+i*26,h=R(12,33);string m=new[]{"Peach stucco","Mint stucco","Ivory stucco"}[i%3];
            Box("Distant apartment",new Vector3(-67,h/2,z),new Vector3(18,h,20),m,backdrop,true);
            for(int f=1;f<h/3.5f;f++)for(int j=0;j<5;j++)Box("Backdrop window",new Vector3(-57.95f,f*3.2f,z-7+j*3.5f),new Vector3(.06f,1.6f,1.7f),"Glass teal",backdrop);
            Box("Roof cornice",new Vector3(-67,h,z),new Vector3(19,.4f,21),"White trim",backdrop);
        }
    }
    static void Hotel(string name,float x,float z,float depth,float width,float height,string facade,string neon,int variant)
    {
        var t=Group("02 | "+name);float front=x+depth/2;
        Box("Hotel mass",new Vector3(x,height/2,z),new Vector3(depth,height,width),facade,t,true);
        Box("Raised foundation",new Vector3(x,.25f,z),new Vector3(depth+.5f,.5f,width+.6f),"Stone",t,true);
        Box("Front plinth",new Vector3(front+.16f,1,z),new Vector3(.32f,1.6f,width),"Stone",t);
        for(float y=4;y<height;y+=3.35f)
        {
            Box("Continuous art deco eyebrow",new Vector3(front+.28f,y+.92f,z),new Vector3(.65f,.16f,width+.5f),"White trim",t);
            for(float dz=-width/2+3;dz<width/2-1.5f;dz+=3.8f)
            {
                if(Mathf.Abs(dz)<2.2f)continue;
                Box("Recessed window surround",new Vector3(front+.045f,y-.05f,z+dz),new Vector3(.12f,2.3f,2.65f),"Seafoam trim",t);
                Box("Tinted glazing",new Vector3(front+.13f,y,z+dz),new Vector3(.08f,1.9f,2.24f),(rng.NextDouble()>.8?"Warm window":"Glass teal"),t);
                Box("Window mullion",new Vector3(front+.21f,y,z+dz),new Vector3(.08f,2,.065f),"White trim",t);
                Box("Window sill",new Vector3(front+.35f,y-1.05f,z+dz),new Vector3(.7f,.16f,2.6f),"White trim",t);
                if(variant%2==1&&Mathf.Abs(dz)>5)
                {
                    Box("Balcony slab",new Vector3(front+.9f,y-1.05f,z+dz),new Vector3(1.8f,.2f,3.2f),"White trim",t);
                    Box("Balcony top rail",new Vector3(front+1.7f,y-.2f,z+dz),new Vector3(.055f,.07f,3.1f),"Bronze",t);
                    for(int j=0;j<9;j++)Box("Balustrade",new Vector3(front+1.7f,y-.55f,z+dz-1.45f+j*.36f),new Vector3(.035f,.9f,.035f),"Bronze",t);
                }
            }
        }
        for(int i=0;i<3;i++)Box("Stepped parapet",new Vector3(x,height+.24f+i*.33f,z),new Vector3(depth+.8f-i*.6f,.35f,width+1-i*.65f),"White trim",t);
        // Projecting vertical sign tower and fluted spine.
        Box("Central stepped tower",new Vector3(front+.5f,height*.55f,z),new Vector3(1.6f,height+3.1f,4.15f),variant==2?"Seafoam trim":"White trim",t);
        for(int i=-1;i<=1;i++)Box("Vertical neon rib",new Vector3(front+1.34f,height*.6f,z+i*.8f),new Vector3(.075f,height-4,.07f),neon,t);
        for(float yy=6;yy<height-1;yy+=3.4f)
        {
            var ring=Cylinder("Art deco porthole rim",new Vector3(front+1.4f,yy,z),.5f,.12f,"Bronze",t);ring.transform.localRotation=Quaternion.Euler(0,0,90);
            var pane=Cylinder("Circular porthole glazing",new Vector3(front+1.48f,yy,z),.38f,.035f,"Glass teal",t);pane.transform.localRotation=Quaternion.Euler(0,0,90);
        }
        if(variant==0||variant==2)
        {
            float zz=z-width*.5f+1.9f;
            Cylinder("Streamline corner tower",new Vector3(front-.3f,height*.5f,zz),2.15f,height,"White trim",t,true);
            for(float yy=4.5f;yy<height;yy+=3.35f)
            {
                Cylinder("Curved teal ribbon",new Vector3(front-.3f,yy,zz),2.18f,.85f,"Seafoam trim",t);
                Cylinder("Curved cornice",new Vector3(front-.3f,yy+.6f,zz),2.35f,.14f,"White trim",t);
            }
        }
        for(int i=0;i<3;i++)Box("Tower crown step",new Vector3(front+.5f,height+1.9f+i*.4f,z),new Vector3(1.6f-i*.25f,.45f,4.3f-i*.9f),"White trim",t);
        Box("Hotel name fascia",new Vector3(front+1.6f,3.75f,z),new Vector3(.35f,1.5f,width-2),"Dark interior",t);
        Text(name,new Vector3(front+1.81f,3.85f,z),1.3f,variant%2==0?"#FF94B8":"#ACFFF1",t);
        Text("O C E A N F R O N T   •   S U I T E S",new Vector3(front+1.82f,3.28f,z),.29f,"#F6D9BA",t);
        // Colonnade and glass lobby front.
        for(float dz=-width/2+2;dz<width/2;dz+=4.5f)
        {
            Box("Ground-floor lobby glass",new Vector3(front+.04f,1.65f,z+dz),new Vector3(.1f,2.5f,3.8f),"Glass teal",t);
            Cylinder("Colonnade pier",new Vector3(front+2.2f,1.8f,z+dz),.17f,3.2f,"White trim",t,true);
        }
        Box("Cantilever canopy",new Vector3(front+1.6f,2.9f,z),new Vector3(4,.2f,width+1.2f),"White trim",t);
        Box("Canopy neon underside",new Vector3(front+3.58f,2.78f,z),new Vector3(.05f,.08f,width+.8f),neon,t);
        for(int i=-1;i<=1;i++)Box("Entry steps",new Vector3(front+3.2f+i*.45f,.14f-i*.04f,z),new Vector3(.48f,.28f-i*.08f,5),"Stone",t,true);
        foreach(float dz in new[]{-width*.38f,width*.38f})Planter(new Vector3(front+3.5f,.4f,z+dz),t);
        for(int i=0;i<3;i++)
        {
            Box("Rooftop AC",new Vector3(x-4,height+1,z-7+i*6),new Vector3(3,1.3f,2.2f),"Stone",t);
            for(int j=0;j<6;j++)Box("AC louvre",new Vector3(x-2.48f,height+1,z-7+i*6-.8f+j*.28f),new Vector3(.025f,.7f,.055f),"Midnight metal",t);
        }
        PointLight(name+" facade glow",new Vector3(front+3,4.8f,z),C(variant%2==0?"#FF80AF":"#7BE5DD"),3,9,t);
    }

    static void Diner()
    {
        var t=Group("02 | SUNSET CLUB and walk-in diner");float x=-34,z=5;
        Box("Diner floor",new Vector3(x,.22f,z),new Vector3(27,.44f,27),"Stone",t,true);
        Box("Back wall",new Vector3(-47,2.6f,z),new Vector3(.4f,5.2f,27),"Peach stucco",t,true);
        foreach(float dz in new[]{-13.3f,13.3f})Box("Side wall",new Vector3(x,2.6f,z+dz),new Vector3(27,5.2f,.4f),"Peach stucco",t,true);
        Box("Flat roof",new Vector3(x,5.2f,z),new Vector3(29,.4f,29),"Coral stucco",t,true);
        Box("Roof edge white",new Vector3(-19.4f,5.15f,z),new Vector3(.3f,.55f,29),"White trim",t);
        Box("Roofline neon",new Vector3(-19.2f,5.35f,z),new Vector3(.09f,.1f,29),"Neon pink",t);
        // Entry remains open. Window bays have physical low walls and upright mullions.
        foreach(float dz in new[]{-9.5f,-5.1f,5.1f,9.5f})
        {
            Box("Diner low wall",new Vector3(-20.6f,.8f,z+dz),new Vector3(.35f,1.2f,4.2f),"Seafoam trim",t,true);
            Box("Diner window",new Vector3(-20.6f,2.5f,z+dz),new Vector3(.1f,2.1f,4.05f),"Glass teal",t,true);
            Box("Chrome mullion",new Vector3(-20.4f,2.6f,z+dz+2),new Vector3(.13f,4.5f,.12f),"Bronze",t);
        }
        Box("Entrance top",new Vector3(-20.5f,4.4f,z),new Vector3(.35f,1.4f,4.5f),"Coral stucco",t,true);
        Box("SUNSET sign backing",new Vector3(-19.2f,6.7f,z),new Vector3(.4f,2.4f,20),"Dark interior",t);
        Text("SUNSET  CLUB",new Vector3(-18.94f,6.9f,z),1.7f,"#FF8FB9",t);
        Text("D I N E R   /   C O C K T A I L S   /   1 9 8 6",new Vector3(-18.93f,5.95f,z),.34f,"#95F3E8",t);
        // Checkerboard walk-in interior, counter, stools and jukebox.
        for(int i=0;i<12;i++)for(int j=0;j<12;j++)Box("Terrazzo checker tile",new Vector3(-46+i*2.1f,.455f,-7+j*2.1f),new Vector3(2.08f,.03f,2.08f),(i+j)%2==0?"Ivory stucco":"Seafoam trim",t);
        Box("Diner counter",new Vector3(-39,1.1f,5),new Vector3(2.4f,1.3f,17),"Coral stucco",t,true);
        Box("Polished counter top",new Vector3(-38.7f,1.83f,5),new Vector3(3.1f,.16f,17.4f),"White trim",t);
        for(int i=0;i<6;i++)
        {
            float zz=-1+i*2.4f;Cylinder("Stool foot",new Vector3(-35.8f,.56f,zz),.4f,.12f,"Midnight metal",t);
            Cylinder("Stool stem",new Vector3(-35.8f,.94f,zz),.07f,.75f,"Bronze",t);
            Cylinder("Stool seat",new Vector3(-35.8f,1.34f,zz),.43f,.18f,"Red fabric",t,true);
        }
        Box("Jukebox body",new Vector3(-23.3f,1.45f,-3.4f),new Vector3(1.05f,2,1.1f),"Wood",t,true);
        Box("Jukebox illuminated panel",new Vector3(-22.74f,1.7f,-3.4f),new Vector3(.07f,1.2f,.84f),"Neon aqua",t);
        for(int i=0;i<5;i++)Box("Jukebox grille",new Vector3(-22.68f,.95f+i*.13f,-3.4f),new Vector3(.05f,.04f,.85f),"Bronze",t);
        Text("RADIO  86",new Vector3(-22.66f,2.1f,-3.4f),.18f,"#31495F",t);
        var interact=new GameObject("Diner Radio | E to interact").transform;interact.SetParent(t,false);interact.position=new Vector3(-21.5f,1.5f,-3.4f);
        foreach(float zz in new[]{-2f,10f})
        {
            Cylinder("Pendant shade",new Vector3(-30,4.3f,zz),.55f,.22f,"Bronze",t);
            Beam("Pendant cable",new Vector3(-30,4.4f,zz),new Vector3(-30,5,zz),.025f,"Midnight metal",t);
            PointLight("Diner pendant",new Vector3(-30,3.9f,zz),C("#FFCD99"),2,10,t);
        }
        for(int i=0;i<3;i++)CafeTable(new Vector3(-15.7f,.4f,-5+i*6),t);
        Text("OPEN LATE",new Vector3(-20.2f,2.9f,15),.55f,"#FBE3B5",t);
    }

    static void Streetscape()
    {
        var t=Group("04 | Palms, lighting and street furniture");
        for(int z=-98;z<=100;z+=15)
        {
            Palm(new Vector3(23,.4f,z+R(-1,1)),R(8,11),t);
            if(z%3==1)Palm(new Vector3(-12,.4f,z+6),R(7,9.5f),t);
            Lamp(new Vector3(17,.4f,z+5),t);
            if(z%2==0){Bench(new Vector3(24.8f,.4f,z+5),t);Bin(new Vector3(22.2f,.4f,z+8),t);}
        }
        for(int z=-91;z<100;z+=30)
        {
            Box("Promenade planting bed",new Vector3(23,.42f,z),new Vector3(3.8f,.12f,5),"Grass",t);
            foreach(float zz in new[]{-2.6f,2.6f})Box("Planting bed stone edge",new Vector3(23,.5f,z+zz),new Vector3(4,.23f,.15f),"Ivory stucco",t);
            foreach(float xx in new[]{21f,25f})Box("Planting bed stone side",new Vector3(xx,.5f,z),new Vector3(.15f,.23f,5.3f),"Ivory stucco",t);
        }
        foreach(float z in new[]{-84f,-47f,22f,59f,94f})Palm(new Vector3(-13,.4f,z),R(7.5f,10),t);
        for(int i=0;i<7;i++)Car(new Vector3(-7.15f,.15f,-89+i*25),i%4,t,0);
        Car(new Vector3(5.4f,.15f,70),2,t,180);
        for(int i=0;i<3;i++)Car(new Vector3(-57,.2f,-85+i*52),i,t,90);
        foreach(int z in new[]{-64,31})
        {
            for(int i=0;i<2;i++)
            {
                float xx=i==0?-10.5f:10.5f;
                Beam("Crosswalk signal pole",new Vector3(xx,.4f,z+3),new Vector3(xx,6.8f,z+3),.08f,"Midnight metal",t);
                Beam("Signal arm",new Vector3(xx,6.8f,z+3),new Vector3(xx+(i==0?4:-4),6.8f,z+3),.075f,"Midnight metal",t);
                Box("Traffic signal",new Vector3(xx+(i==0?4:-4),6.25f,z+3),new Vector3(.48f,1.05f,.43f),"Midnight metal",t);
                Shape("Red signal lens",PrimitiveType.Sphere,new Vector3(xx+(i==0?4:-4),6.5f,z+2.76f),new Vector3(.2f,.2f,.04f),"Tail light",t);
                Shape("Green signal lens",PrimitiveType.Sphere,new Vector3(xx+(i==0?4:-4),6.02f,z+2.76f),new Vector3(.2f,.2f,.04f),"Neon aqua",t);
            }
            Beam("Street sign post",new Vector3(11,.4f,z+5),new Vector3(11,3.4f,z+5),.045f,"Bronze",t);
            Box("Ocean Drive sign plate",new Vector3(11,3.25f,z+5),new Vector3(.15f,.44f,2.4f),"Seafoam trim",t);
            Text("OCEAN DRIVE",new Vector3(11.1f,3.25f,z+5),.19f,"#F4F1DA",t);
        }
        // Low boulevard gateway, establishes a clear southern entrance.
        var gateway=Group("04 | Welcome monument");
        Box("Monument footing",new Vector3(23,.85f,-92),new Vector3(4,1.3f,1.2f),"Ivory stucco",gateway,true);
        Box("Deco sign pylon",new Vector3(23,2.8f,-92),new Vector3(3.6f,2.6f,.5f),"Seafoam trim",gateway,true);
        Text("PALM\nHARBOR",new Vector3(23,3,-92.28f),.75f,"#FFDDD7",gateway,0);
        Text("EST. 1986",new Vector3(23,1.85f,-92.28f),.24f,"#FFF0C7",gateway,0);
        for(int i=0;i<3;i++)Box("Monument deco stripe",new Vector3(23,4.3f+i*.16f,-92),new Vector3(4-i*.45f,.08f,.6f),"Neon pink",gateway);
    }

    static void Palm(Vector3 p,float height,Transform parent)
    {
        var t=Group("Royal palm",parent);t.localPosition=p;float lean=R(-.8f,.8f);Vector3 last=Vector3.zero;
        for(int i=1;i<=9;i++)
        {
            float h=i/9f;Vector3 next=new Vector3(lean*h*h,height*h,.35f*h*h);
            Beam("Segmented trunk",last,next,Mathf.Lerp(.23f,.13f,h),"Palm bark",t);
            if(i<9){var ring=Cylinder("Trunk growth ring",next,.24f-h*.1f,.055f,"Wood",t);ring.transform.up=(next-last).normalized;}
            last=next;
        }
        Shape("Palm crown",PrimitiveType.Sphere,last,new Vector3(.65f,.8f,.65f),"Palm dark",t);
        var verts=new List<Vector3>();var tris=new List<int>();
        for(int f=0;f<10;f++)
        {
            float angle=f*Mathf.PI*.2f+R(-.1f,.1f),length=R(3.1f,4.4f);Vector3 dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),side=new Vector3(-dir.z,0,dir.x);
            Vector3 prev=last;
            for(int i=1;i<=9;i++)
            {
                float q=i/9f;Vector3 center=last+dir*(q*length)+Vector3.up*(Mathf.Sin(q*Mathf.PI)*1.1f-q*q*1.4f);
                Beam("Frond midrib",prev,center,.025f*(1-q)+.008f,"Palm dark",t);
                float previousQ=(i-1)/9f;
                float previousWidth=Mathf.Pow(Mathf.Sin(previousQ*Mathf.PI),.8f)*.5f+.025f;
                float currentWidth=Mathf.Pow(Mathf.Sin(q*Mathf.PI),.8f)*.5f+.015f;
                int ribbon=verts.Count;
                verts.Add(prev-side*previousWidth);verts.Add(prev+side*previousWidth);
                verts.Add(center-side*currentWidth);verts.Add(center+side*currentWidth);
                tris.AddRange(new[]{ribbon,ribbon+2,ribbon+1,ribbon+1,ribbon+2,ribbon+3,ribbon+1,ribbon+2,ribbon,ribbon+3,ribbon+2,ribbon+1});
                for(int s=-1;s<=1;s+=2)
                {
                    float blade=1.1f*Mathf.Sin(q*Mathf.PI)+.12f;Vector3 tip=center+side*(s*blade)+dir*.55f-Vector3.up*(.16f+q*.18f);
                    int n=verts.Count;verts.Add(center-dir*.16f);verts.Add(tip);verts.Add(center+dir*.2f);verts.Add((center+tip)*.5f+Vector3.up*.06f);
                    tris.AddRange(new[]{n,n+3,n+1,n+3,n+2,n+1,n+1,n+3,n,n+1,n+2,n+3});
                }
                prev=center;
            }
        }
        var mesh=new Mesh{name="Pinnate palm foliage"};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.normals=Enumerable.Repeat(Vector3.up,verts.Count).ToArray();
        var leaves=new GameObject("Individual arching palm leaflets");leaves.transform.SetParent(t,false);leaves.AddComponent<MeshFilter>().sharedMesh=mesh;leaves.AddComponent<MeshRenderer>().sharedMaterial=mats["Palm leaf"];leaves.isStatic=true;
        Cylinder("Palm tree soil grate",new Vector3(0,.015f,0),.75f,.04f,"Dark interior",t);
    }
    static void Planter(Vector3 p,Transform t)
    {
        Box("Terrazzo planter",p+Vector3.up*.35f,new Vector3(1.3f,.7f,1.3f),"Ivory stucco",t,true);
        Cylinder("Planter soil",p+Vector3.up*.72f,.52f,.04f,"Palm bark",t);
        for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var leaf=Shape("Agave leaf",PrimitiveType.Sphere,p+new Vector3(Mathf.Cos(a)*.25f,1.03f,Mathf.Sin(a)*.25f),new Vector3(.12f,.85f,.28f),"Palm leaf",t);leaf.transform.rotation=Quaternion.Euler(Mathf.Sin(a)*35,0,Mathf.Cos(a)*35);}
    }
    static void Lamp(Vector3 p,Transform t)
    {
        Cylinder("Streetlamp base",p+Vector3.up*.2f,.24f,.4f,"Midnight metal",t,true);
        Beam("Slender bronze lamp",p,p+Vector3.up*6.2f,.065f,"Bronze",t);
        Box("Deco lamp cap",p+Vector3.up*6.1f,new Vector3(.72f,.12f,.72f),"Midnight metal",t);
        Box("Lantern opal lens",p+Vector3.up*5.8f,new Vector3(.38f,.48f,.38f),"Neon amber",t);
        PointLight("Promenade lantern",p+Vector3.up*5.6f,C("#FFD3A1"),2.3f,11,t);
    }
    static void PointLight(string name,Vector3 p,Color color,float intensity,float range,Transform t)
    {
        var go=new GameObject(name);go.transform.SetParent(t,false);go.transform.localPosition=p;var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;accentLights.Add(l);
    }
    static void Bench(Vector3 p,Transform t)
    {
        for(int i=0;i<4;i++)Box("Bench seat slat",p+new Vector3(i*.16f,.52f,0),new Vector3(.13f,.09f,1.9f),"Wood",t,true);
        for(int i=0;i<3;i++)Box("Bench back slat",p+new Vector3(.59f,.74f+i*.15f,0),new Vector3(.08f,.11f,1.9f),"Wood",t);
        foreach(float zz in new[]{-.72f,.72f}){Box("Bench leg",p+new Vector3(.25f,.25f,zz),new Vector3(.1f,.5f,.1f),"Midnight metal",t);Box("Bench back leg",p+new Vector3(.57f,.48f,zz),new Vector3(.08f,.95f,.08f),"Midnight metal",t);}
    }
    static void Bin(Vector3 p,Transform t)
    {
        Cylinder("Litter bin",p+Vector3.up*.5f,.31f,1,"Seafoam trim",t,true);Cylinder("Bin lid",p+Vector3.up*1.01f,.34f,.1f,"Midnight metal",t);
    }
    static void CafeTable(Vector3 p,Transform t)
    {
        Cylinder("Cafe table top",p+Vector3.up*.82f,.72f,.08f,"Ivory stucco",t,true);Cylinder("Cafe table pedestal",p+Vector3.up*.42f,.06f,.8f,"Bronze",t);
        foreach(int side in new[]{-1,1})
        {
            Cylinder("Terrace chair seat",p+new Vector3(0,.46f,side*1.1f),.34f,.08f,"Aqua fabric",t,true);
            for(int i=-1;i<=1;i+=2)for(int j=-1;j<=1;j+=2)Beam("Chair leg",p+new Vector3(i*.22f,0,side*1.1f+j*.2f),p+new Vector3(i*.22f,.45f,side*1.1f+j*.2f),.022f,"Bronze",t);
            Box("Chair back",p+new Vector3(0,.78f,side*1.4f),new Vector3(.62f,.45f,.08f),"Aqua fabric",t);
        }
        Cylinder("Drinking glass",p+new Vector3(.22f,.97f,.2f),.065f,.22f,"Warm window",t);
        Cylinder("Table candle",p+new Vector3(-.15f,.95f,-.1f),.06f,.18f,"Neon amber",t);
    }
    static void Car(Vector3 p,int style,Transform parent,float yaw)
    {
        var t=Group("1980s coastal coupe",parent);t.localPosition=p;t.localRotation=Quaternion.Euler(0,yaw,0);string paint=new[]{"Car coral","Car cream","Car turquoise","Car purple"}[style%4];
        Box("Chassis",new Vector3(0,.47f,0),new Vector3(1.95f,.45f,4.65f),paint,t,true);
        Box("Beltline",new Vector3(0,.78f,0),new Vector3(1.98f,.35f,4.5f),paint,t);
        Box("Long bonnet",new Vector3(0,.97f,1.25f),new Vector3(1.9f,.13f,1.9f),paint,t);
        Box("Trunk lid",new Vector3(0,.98f,-1.7f),new Vector3(1.9f,.12f,1.08f),paint,t);
        var cabin=Box("Raked glass cabin",new Vector3(0,1.3f,-.36f),new Vector3(1.65f,.7f,1.95f),"Glass teal",t);cabin.transform.localRotation=Quaternion.Euler(-7,0,0);
        Box("Hardtop",new Vector3(0,1.67f,-.4f),new Vector3(1.7f,.12f,1.65f),paint,t);
        foreach(float xx in new[]{-.84f,.84f})
        {
            Box("B pillar",new Vector3(xx,1.32f,-.49f),new Vector3(.09f,.65f,.09f),"Midnight metal",t);
            Box("Side chrome",new Vector3(xx*1.18f,.79f,0),new Vector3(.035f,.035f,4.3f),"Bronze",t);
            Box("Door handle",new Vector3(xx*1.2f,1.03f,-.55f),new Vector3(.07f,.05f,.24f),"Bronze",t);
            Box("Wing mirror",new Vector3(xx*1.25f,1.18f,.5f),new Vector3(.24f,.15f,.26f),paint,t);
        }
        foreach(float xx in new[]{-1f,1f})foreach(float zz in new[]{-1.5f,1.48f})
        {
            var tyre=Cylinder("Rubber tyre",new Vector3(xx,.43f,zz),.41f,.22f,"Tyre",t);tyre.transform.localRotation=Quaternion.Euler(0,0,90);
            var hub=Cylinder("Alloy wheel",new Vector3(xx*1.12f,.43f,zz),.24f,.018f,"Bronze",t);hub.transform.localRotation=Quaternion.Euler(0,0,90);
        }
        foreach(float zz in new[]{-2.33f,2.33f})Box("Chrome bumper",new Vector3(0,.51f,zz),new Vector3(2,.15f,.16f),"Bronze",t);
        foreach(float xx in new[]{-.66f,.66f})
        {
            Box("Rectangular headlights",new Vector3(xx,.82f,2.265f),new Vector3(.5f,.22f,.045f),"Headlamp",t);
            Box("Tail lamps",new Vector3(xx,.85f,-2.265f),new Vector3(.6f,.18f,.045f),"Tail light",t);
        }
        Box("Front grille",new Vector3(0,.8f,2.27f),new Vector3(.6f,.22f,.05f),"Midnight metal",t);
        Box("Licence plate",new Vector3(0,.54f,-2.425f),new Vector3(.52f,.17f,.015f),"White trim",t);
        Text("PALM 86",new Vector3(0,.55f,-2.44f),.056f,"#2D3C50",t,0);
    }

    static void Beach()
    {
        var t=Group("05 | South Beach recreation");
        for(int i=0;i<7;i++)
        {
            Vector3 p=new Vector3(39+(i%2)*11,.05f,-80+i*26);Umbrella(p,i%2==0?"Red fabric":"Aqua fabric",t);
            Lounge(p+new Vector3(-1.8f,0,1.5f),t);Lounge(p+new Vector3(1.5f,0,1.5f),t);
        }
        for(int i=0;i<9;i++)Palm(new Vector3(31+R(0,6),.15f,-98+i*24),R(6,8),t);
        // Iconic pastel lifeguard hut, steps, hand rails and surfboards.
        var hut=Group("Lifeguard station 03",t);hut.localPosition=new Vector3(60,0,28);hut.localRotation=Quaternion.Euler(0,-15,0);
        foreach(float xx in new[]{-2.1f,2.1f})foreach(float zz in new[]{-1.6f,1.6f})Box("Timber stilts",new Vector3(xx,1.4f,zz),new Vector3(.18f,2.8f,.18f),"Wood",hut,true);
        Box("Raised lookout deck",new Vector3(0,2.65f,0),new Vector3(5.9f,.25f,5),"White trim",hut,true);
        Box("Cabin",new Vector3(0,4.05f,.4f),new Vector3(4.3f,2.65f,3.4f),"Aqua fabric",hut,true);
        for(int i=0;i<10;i++)Box("Horizontal clapboard",new Vector3(0,2.9f+i*.23f,-1.32f),new Vector3(4.35f,.035f,.035f),"White trim",hut);
        Box("Lookout window",new Vector3(0,4.4f,-1.36f),new Vector3(3.3f,1.05f,.05f),"Glass teal",hut);
        Box("Coral roof",new Vector3(0,5.5f,.2f),new Vector3(5.8f,.25f,4.8f),"Red fabric",hut);
        Box("Roof stripe",new Vector3(0,5.32f,-2.2f),new Vector3(5.8f,.12f,.1f),"White trim",hut);
        Text("LIFEGUARD  03",new Vector3(0,3.35f,-1.4f),.32f,"#F5E7C2",hut,0);
        for(int i=0;i<9;i++)Box("Lifeguard stair",new Vector3(0,.15f+i*.29f,-6.3f+i*.45f),new Vector3(1.4f,.3f,.5f),"Wood",hut,true);
        foreach(int s in new[]{-1,1})Beam("Stair handrail",new Vector3(s*.78f,1,-6.5f),new Vector3(s*.78f,3.4f,-2.8f),.04f,"White trim",hut);
        foreach(float xx in new[]{-2.8f,2.8f}){Beam("Deck rail",new Vector3(xx,3.6f,-2.4f),new Vector3(xx,3.6f,2.4f),.035f,"White trim",hut);for(int k=0;k<8;k++)Beam("Deck baluster",new Vector3(xx,2.75f,-2.4f+k*.65f),new Vector3(xx,3.6f,-2.4f+k*.65f),.025f,"White trim",hut);}
        for(int i=0;i<2;i++){var surf=Shape("Rescue surfboard",PrimitiveType.Capsule,new Vector3(3.5f+i*.6f,1.8f,.6f),new Vector3(.65f,1.7f,.18f),i==0?"Red fabric":"Road paint",hut);surf.transform.localRotation=Quaternion.Euler(0,0,-13+i*22);}
        Beam("Flagpole",new Vector3(2,5.5f,.5f),new Vector3(2,8,.5f),.025f,"Bronze",hut);Box("Swimming flag",new Vector3(2.65f,7.5f,.5f),new Vector3(1.3f,.7f,.015f),"Aqua fabric",hut);
        // Boardwalk spur gives the player a strong view towards the water.
        for(int i=0;i<45;i++)Box("Beach access plank",new Vector3(27+i*.85f,.1f,-35),new Vector3(.81f,.14f,3),"Wood",t,true);
        foreach(float zz in new[]{-36.6f,-33.4f})for(int i=0;i<9;i++){Beam("Boardwalk marker post",new Vector3(27+i*4.5f,0,zz),new Vector3(27+i*4.5f,.7f,zz),.055f,"White trim",t);}
        // Sand volleyball court.
        foreach(float xx in new[]{44f,60f})Beam("Court sideline",new Vector3(xx,.11f,64),new Vector3(xx,.11f,82),.025f,"White paint",t);
        foreach(float zz in new[]{64f,82f})Beam("Court baseline",new Vector3(44,.11f,zz),new Vector3(60,.11f,zz),.025f,"White paint",t);
        foreach(float xx in new[]{43f,61f})Beam("Net upright",new Vector3(xx,0,73),new Vector3(xx,3,73),.055f,"White trim",t);
        for(int i=0;i<5;i++)Beam("Net horizontal",new Vector3(43,1.5f+i*.25f,73),new Vector3(61,1.5f+i*.25f,73),.009f,"Ivory stucco",t);
        for(int i=0;i<45;i++)Beam("Net mesh",new Vector3(43+i*.4f,1.5f,73),new Vector3(43+i*.4f,2.5f,73),.008f,"Ivory stucco",t);
    }
    static void Umbrella(Vector3 p,string fabric,Transform parent)
    {
        var t=Group("Striped beach parasol",parent);t.localPosition=p;Beam("Umbrella pole",Vector3.zero,Vector3.up*2.8f,.035f,"White trim",t);
        var v=new List<Vector3>();var trianglesA=new List<int>();var trianglesB=new List<int>();
        for(int i=0;i<16;i++)
        {
            float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;int k=v.Count;
            v.Add(new Vector3(0,2.9f,0));v.Add(new Vector3(Mathf.Cos(a)*2,2.3f,Mathf.Sin(a)*2));v.Add(new Vector3(Mathf.Cos(b)*2,2.3f,Mathf.Sin(b)*2));
            (i%2==0?trianglesA:trianglesB).AddRange(new[]{k,k+2,k+1,k+1,k+2,k});
            Beam("Canopy rib",new Vector3(0,2.87f,0),v[k+1],.012f,"White trim",t);
        }
        var mesh=new Mesh{name="Striped parasol canopy"};mesh.SetVertices(v);mesh.subMeshCount=2;mesh.SetTriangles(trianglesA,0);mesh.SetTriangles(trianglesB,1);mesh.normals=Enumerable.Repeat(Vector3.up,v.Count).ToArray();
        var go=new GameObject("Alternating fabric panels");go.transform.SetParent(t,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=new[]{mats[fabric],mats["White trim"]};go.isStatic=true;
    }
    static void Lounge(Vector3 p,Transform t)
    {
        Box("Lounge canvas",p+Vector3.up*.38f,new Vector3(.75f,.08f,1.65f),"White trim",t,true);
        var back=Box("Angled lounge back",p+new Vector3(0,.68f,.99f),new Vector3(.75f,.07f,.9f),"Aqua fabric",t);back.transform.localRotation=Quaternion.Euler(-42,0,0);
        foreach(float x in new[]{-.4f,.4f}){Beam("Lounge frame",p+new Vector3(x,.35f,-.9f),p+new Vector3(x,.35f,1.2f),.025f,"Bronze",t);foreach(float z in new[]{-.55f,.55f})Beam("Lounge foot",p+new Vector3(x,0,z),p+new Vector3(x,.35f,z+.18f),.025f,"Bronze",t);}
    }

    static void LightingAndPlayer()
    {
        var t=Group("06 | Golden hour atmosphere");
        var sunGO=new GameObject("Sun | Miami golden hour");sunGO.transform.SetParent(t,false);sunGO.transform.rotation=Quaternion.Euler(13,-105,0);
        var sun=sunGO.AddComponent<Light>();sun.type=LightType.Directional;sun.color=C("#FFD4AC");sun.intensity=1.5f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.75f;sun.shadowBias=.035f;sun.shadowNormalBias=.3f;RenderSettings.sun=sun;
        var sky=new Material(Shader.Find("PalmHarbor/SunsetSky")??Shader.Find("Skybox/Procedural"));sky.name="Palm Harbor sunset";
        if(sky.HasProperty("_TopColor")){sky.SetColor("_TopColor",C("#5F7FA9"));sky.SetColor("_HorizonColor",C("#F3B6A5"));sky.SetColor("_BottomColor",C("#C6A7AA"));sky.SetVector("_SunDirection",new Vector4(.95f,.12f,.26f,0));sky.SetColor("_SunColor",C("#FFE3AC"));sky.SetFloat("_SunSize",2.5f);}
        sky=SaveAsset(sky,Root+"/Materials/Palm Harbor sunset.mat");RenderSettings.skybox=sky;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=C("#9CAFCB");RenderSettings.ambientEquatorColor=C("#B5A2B4");RenderSettings.ambientGroundColor=C("#6E6777");RenderSettings.ambientIntensity=1;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=C("#D0B0B2");RenderSettings.fogDensity=.0019f;
        var pipeline=UniversalRenderPipeline.asset;if(pipeline){pipeline.supportsHDR=true;pipeline.shadowDistance=110;pipeline.renderScale=1;EditorUtility.SetDirty(pipeline);}
        QualitySettings.shadowResolution=UnityEngine.ShadowResolution.High;QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.antiAliasing=4;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Coastal film grade";
        var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.1f;bloom.intensity.value=.28f;bloom.scatter.value=.6f;
        var tonemap=profile.Add<Tonemapping>(true);tonemap.mode.value=TonemappingMode.ACES;
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.value=.25f;color.contrast.value=9;color.saturation.value=7;
        var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.14f;vignette.smoothness.value=.5f;
        profile=SaveAsset(profile,Root+"/Materials/Coastal film grade.asset");
        foreach(var component in profile.components)if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);
        var vg=new GameObject("Global Volume | Bloom, ACES and colour grade");vg.transform.SetParent(t,false);var volume=vg.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        var player=new GameObject("PLAYER | Walk the coastal district");player.transform.position=new Vector3(14,.4f,-80);player.transform.rotation=Quaternion.Euler(0,-30,0);
        var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=new Vector3(0,.9f,0);cc.stepOffset=.4f;cc.slopeLimit=48;
        var cg=new GameObject("Main Camera");cg.tag="MainCamera";cg.transform.SetParent(player.transform,false);cg.transform.localPosition=new Vector3(0,1.65f,0);
        var cam=cg.AddComponent<Camera>();cam.fieldOfView=65;cam.nearClipPlane=.08f;cam.farClipPlane=700;cam.allowHDR=true;cg.AddComponent<AudioListener>();
        var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;data.volumeLayerMask=1;
        var explorer=player.AddComponent<PalmHarborExplorer>();explorer.viewCamera=cam;explorer.sun=sun;explorer.skyMaterial=sky;explorer.neonLights=accentLights.ToArray();explorer.interactionPoint=GameObject.Find("Diner Radio | E to interact").transform;
        // A soft environmental bed, entirely synthesized from noise rather than imported audio.
        var ambience=player.AddComponent<AudioSource>();ambience.clip=OceanAmbience();ambience.loop=true;ambience.volume=.085f;ambience.spatialBlend=0;ambience.playOnAwake=true;
        Application.runInBackground=true;PlayerSettings.productName="Palm Harbor 1986";PlayerSettings.companyName="Virtual Environment Studio";
        DynamicGI.UpdateEnvironment();
    }
    static AudioClip OceanAmbience()
    {
        const int rate=22050, seconds=12;var samples=new float[rate*seconds];float smooth=0;
        for(int i=0;i<samples.Length;i++){float n=(float)rng.NextDouble()*2-1;smooth=Mathf.Lerp(smooth,n,.045f);float wave=.45f+.35f*Mathf.Sin(i/(float)rate*.7f);samples[i]=smooth*wave;}
        var clip=AudioClip.Create("Atlantic shore ambience",samples.Length,1,rate,false);clip.SetData(samples,0);
        // AudioClip.Create cannot be stored by AssetDatabase reliably; persist as WAV and import.
        var path=Root+"/Textures/Atlantic Shore.wav";
        using(var w=new System.IO.BinaryWriter(System.IO.File.Create(path)))
        {w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples.Length*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(samples.Length*2);foreach(float v in samples)w.Write((short)(Mathf.Clamp(v,-1,1)*32767));}
        AssetDatabase.ImportAsset(path);UnityEngine.Object.DestroyImmediate(clip);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    static T SaveAsset<T>(T obj,string path) where T : UnityEngine.Object
    {
        obj.name=System.IO.Path.GetFileNameWithoutExtension(path);
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        // Mesh native buffers must be recreated on regeneration; CopySerialized leaves stale GPU buffers.
        if(existing is Mesh){AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(obj,path);return obj;}
        // Preserve the profile GUID while replacing its generated component subassets.
        if(existing is VolumeProfile)
            foreach(var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                if(subAsset is VolumeComponent)UnityEngine.Object.DestroyImmediate(subAsset,true);
        if(existing){EditorUtility.CopySerialized(obj,existing);UnityEngine.Object.DestroyImmediate(obj);}
        else {AssetDatabase.CreateAsset(obj,path);existing=obj;}
        return existing;
    }
    static void CombineGeometry()
    {
        // Merge decorative render meshes by material per district group, retaining original colliders and semantic hierarchy.
        foreach(Transform group in world)
        {
            var renderers=group.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<MeshFilter>()&&r.gameObject.isStatic).ToArray();
            var bins=new Dictionary<Material,List<CombineInstance>>();
            foreach(var renderer in renderers)
            {
                var filter=renderer.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;if(!mesh)continue;
                for(int i=0;i<mesh.subMeshCount;i++)
                {
                    var material=renderer.sharedMaterials[Mathf.Min(i,renderer.sharedMaterials.Length-1)];if(!material)continue;
                    if(!bins.ContainsKey(material))bins[material]=new List<CombineInstance>();
                    bins[material].Add(new CombineInstance{mesh=mesh,subMeshIndex=i,transform=group.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            foreach(var pair in bins)
            {
                var mesh=new Mesh{name=group.name+" | "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=Root+"/Meshes/Batch_"+(meshIndex++)+".asset";SaveAsset(mesh,path);mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var g=new GameObject("Render batch | "+pair.Key.name);g.transform.SetParent(group,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
                // Already merged: a second runtime static-batching pass is unnecessary.
                g.isStatic=false;
            }
            foreach(var renderer in renderers){UnityEngine.Object.DestroyImmediate(renderer.GetComponent<MeshFilter>());UnityEngine.Object.DestroyImmediate(renderer);}
        }
    }
}
