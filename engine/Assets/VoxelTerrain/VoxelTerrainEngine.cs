using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System;
using System.Linq;
using PlayerPrefs = VoxelEngine.SaverVoxel;
using VoxelEngine.DoubleKey;
namespace VoxelEngine{
	public enum EditorPlay{
		isEditor = 0,
		isPlaying = 1,
	}
	[RequireComponent (typeof (TerrainUpdate))]
	[RequireComponent (typeof (TerrainEditorUpdate))]

public class VoxelTerrainEngine: MonoBehaviour{
		
	public int seed;
		float timer;
	[Range(-10,10)]
		[Tooltip("value to set Voxel")]public int value;
		[Range(0,4)]
		[Tooltip("value to set Voxel")]public byte VoxelMaterial;
		[Range(0,255)]
		[Tooltip("Area of effect")]public float effectArea;

	[Tooltip("Save Terrain?")]
	public bool Save;
	[Tooltip("are you using original shader on terrain?")]
		public EditorPlay playMode;
	public bool UseOrignalShader;
	public Texture []textures;
	public Texture[] Normals;
	public static bool HasTrees;
	public static bool HasGrass;
	public NoiseModule noise;
	public UnityEngine.Rendering.ShadowCastingMode shadows;

	public LayerMask mask;
	public Material m_material;
	public int distanceToLoad;
	[Range(0,200)]
	public float DetailDistance;
	//[Range(1,9)]
	//public int maxLod;
	[Range(0,9)]
	public int minLod;
	public int MaxTrees;
	public int MaxGrass;
	public bool MakeCaves;
	public GameObject []Trees;
	public GameObject[] Grass;
	public float []GrassWeights;
	[HideInInspector]
	public Transform Parent;
	public int m_voxelWidthLength = 32, m_voxelHeight = 128;
	public Thread[] thread;
	public static VoxelTerrainEngine Generator;
	bool CanGenerate;
		public static Queue<VoxelChunk>MeshChunks = new Queue<VoxelChunk>();
		public static Queue<VoxelChunk>GenerateVertices = new Queue<VoxelChunk>();
		public static Queue<VoxelChunk>GenerateVoxels = new Queue<VoxelChunk>();
		public static Queue<VoxelChunk>EditedChunks = new Queue<VoxelChunk>();
		public static Queue<VoxelChunk>Trash = new Queue<VoxelChunk>();
		public static Queue<myAction>Createvertices = new Queue<myAction>();
		public static Queue<myAction>EditedActions = new Queue<myAction>();
		public static List<VoxelChunk>ActiveChunks = new List<VoxelChunk>();
		public static DoubleKeyDictionary<Vector3 ,Vector3,VoxelChunk> m_voxelChunk = new DoubleKeyDictionary<Vector3, Vector3, VoxelChunk>();

	
	public WindZone Zone;
	public Transform Target;
	public static int[] LodDistances = new int[9]{
			50,100 ,200,400,800,1600,3200,6400,12800
		};
	public static int [] TriSize = new int[9]{
			1,2,4,8,16,32,64,128,256
		};
		public delegate void myAction();

public void Initialize(){
			ActiveChunks = new List<VoxelChunk>();
			MeshChunks = new Queue<VoxelChunk>();
			GenerateVertices = new Queue<VoxelChunk>();
			GenerateVoxels = new Queue<VoxelChunk>();
			EditedChunks = new Queue<VoxelChunk>();
			Trash = new Queue<VoxelChunk>();
			Createvertices = new Queue<myAction>();
			EditedActions = new Queue<myAction>();
			m_voxelChunk = new DoubleKeyDictionary<Vector3, Vector3, VoxelChunk>();
			if(UseOrignalShader){
				m_material.SetTexture("_TextureOne",textures[0]);
				m_material.SetTexture("_TextureTwo",textures[1]);
				m_material.SetTexture("_TextureThree",textures[2]);
				m_material.SetTexture("_TextureFour",textures[3]);
				m_material.SetTexture("_TextureOneN",Normals[0]);
				m_material.SetTexture("_TextureTwoN",Normals[1]);
				m_material.SetTexture("_TextureThreeN",Normals[2]);
				m_material.SetTexture("_TextureFourN",Normals[3]);
			}
			if(Grass.Length>0)
			for(int i = 0; i < Grass.Length;i++){
				GameObject gameo = Instantiate(Grass[i],new Vector3(0,10000,0),Quaternion.identity)as GameObject;
				Grass[i]= gameo;
			}


				if(Zone==null){
				Debug.LogError("No WindZone detected will attempt to find one in the scene");
				Zone = GameObject.FindObjectOfType<WindZone>();
				if(Zone==null)
					Debug.LogError("No WindZone Found Please add one to the scene");
			}
			for(int i = 0; i < Trees.Length;i++){
				if(Trees[i]==null){
					Debug.LogError("One Tree in the array is null please fix and start again ");
					return;
			}
			}
			for(int i = 0; i < Grass.Length;i++){
				if(Grass[i]==null){
					Debug.LogError("One Grass mesh in the array is null please fix and start again ");
					return;
				}
			}
			if(Grass.Length==0){
				HasGrass = false;
			}
			else HasGrass = true;

			if(Trees.Length==0){
				HasTrees = false;
			}else HasTrees = true;

			if(Grass.Length!=GrassWeights.Length && HasGrass){
				Debug.LogError("Grass Length does not equal GrassWeights Length , will assign the extra weights to 0");
				float[] grassW = GrassWeights;
				GrassWeights = new float[Grass.Length];
				for(int i =0;i < GrassWeights.Length;i++){
					if(i < grassW.Length)
					GrassWeights[i] = grassW[i];
					else GrassWeights[i] = 0;
				}
			}
			if(GrassWeights.Length > 0 && HasGrass==false){
				GrassWeights = null;
			}

				Generator = this;

				Parent = transform;

				VoxelChunk.parent = transform;

				VoxelChunk.generator = Generator;

				MeshFactory.generator = Generator;

				MeshFactory.MakeCaves = MakeCaves;
				
				MarchingCubes.SetTarget(126);

				MarchingCubes.SetWindingOrder(0, 1, 2);

				CanGenerate = true;
			if(thread==null)
				thread = new Thread[Environment.ProcessorCount];
			for(int i = 0;i < thread.Length;i++){
			if(thread[i]!=null)
				thread[i].Abort();
			}

			
				StartThreads();
	}

		//start all threads
public void StartThreads(){
			for(int i = 0;i < thread.Length;i++){

			
			thread[i]= new Thread(Thread1);
				thread[i].IsBackground=true;
				thread[i].Priority = System.Threading.ThreadPriority.Normal;
				thread[i].Start();


			Debug.Log("Thread Started");
			}

		}

		public void EndAllThreads(){
			if(thread!=null)
			for(int i = 0;i < thread.Length;i++){
					if(thread[i]!=null)
					thread[i].Abort();
					Debug.Log("Thread Aborted");
			}

		}
		//raycast for the voxel
		//basically just a normal raycast but with better targetting for the voxels
public static bool RaycastVoxels(Ray ray, out RaycastHit hitinfo,float distance,LayerMask mask ){

				RaycastHit hit;

			if(Physics.Raycast(ray.origin,ray.direction, out hit, distance ,mask )){
				
				hit.point = new Vector3(Mathf.RoundToInt(hit.point.x+(ray.direction.x/2))
				,Mathf.RoundToInt(hit.point.y+(ray.direction.y/2)),Mathf.RoundToInt(hit.point.z
				+(ray.direction.z/2)));

				hitinfo = hit;

				return true;
			}
		
			else {

				hitinfo = hit;

				return false;
			}

		}
		//raycast for the voxel
		//basically just a normal raycast but with better targetting for the voxels
public static bool RaycastVoxels(Ray ray, out RaycastHit hitinfo,float distance ){

				RaycastHit hit;

			if(Physics.Raycast(ray.origin,ray.direction, out hit, distance )){
				
				hit.point = new Vector3(Mathf.RoundToInt(hit.point.x+(ray.direction.x/2))
				,Mathf.RoundToInt(hit.point.y+(ray.direction.y/2)),Mathf.RoundToInt(hit.point.z
				+(ray.direction.z/2)));
				
				hitinfo = hit;

				return true;
			}
			
			else {

				hitinfo = hit;

				return false;
			}
			
		}
		//static method for setting voxel values 
		//can be used for adding or removing voxels
		//once this method is called the chunk its on will
		//be automatically located and set to re create the mesh using using new voxel data

		/// <summary>
		/// Sets the voxels.
		/// </summary>
		/// <param name="hitpoint">Hitpoint.</param>
		/// <param name="value">Value.</param>
		/// <param name="dist">Dist.</param>
		/// <param name="height">Height.</param>
		public static void SetVoxels(Vector3 hitpoint,float value,float dist){
			
			for(int i = 0;i < ActiveChunks.Count;i++){
				VoxelChunk chunk = ActiveChunks[i];

				if(chunk.SetVoxels(hitpoint,value,dist)){
						//Create the voxel data		
						//set various flags so the engine knows it needs to create a new mesh for terrain
						chunk.hascollider=false;
						EditedActions.Enqueue(chunk.CreateVertices);
						
					}

	}
		}

		public static void setMaterial(Vector3 hitpoint,byte value,float dist){
			for(int i = 0;i < ActiveChunks.Count;i++){

				VoxelChunk chunk = ActiveChunks[i];

				if(chunk.SetMaterial(hitpoint,value,dist)){
					//Create the voxel data		
					//set various flags so the engine knows it needs to create a new mesh for terrain
					chunk.hascollider=false;
					EditedActions.Enqueue(chunk.CreateVertices);

				}
			}
		}



		/// <summary>
		/// Sets the Terrain loading distance.
		/// </summary>
		/// <param name="Distance">Distance.</param>
static void SetDistance(int Distance){
			Generator.distanceToLoad = Distance;
		}
		/// <summary>
		/// Sets the detail distance.
		/// </summary>
		/// <param name="Distance">Distance.</param>
static void SetDetailDistance(float Distance){
			Generator.DetailDistance = Distance;
		}

		//method for saving changes to terrain at runtime
public void SaveTerrains(){
			Debug.Log("saved Terrain");
			for(int i = 0;i < ActiveChunks.Count;i++){
			VoxelChunk chunk = ActiveChunks[i];

				if(chunk.HasChanged && chunk.shouldrender){
				chunk.SaveVoxels();
		}
	}
}
			
		public void UpdateTerrain(){
			if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && playMode== EditorPlay.isEditor){
				Transform[] gamos = GetComponentsInChildren<Transform>();
				CanGenerate=false;
				EndAllThreads();
				for(int i = 0;i < gamos.Length;i++){
					if(gamos[i]!=transform){
						DestroyImmediate(gamos[i].gameObject);

					}
				}
				SaveTerrains();
			playMode = EditorPlay.isPlaying;}
			if(playMode== EditorPlay.isEditor){
			if(UnityEditor.EditorApplication.isCompiling==false && CanGenerate==false){

				Transform[] gamos = GetComponentsInChildren<Transform>();
				for(int i = 0;i < gamos.Length;i++){
					if(gamos[i]!=transform){
						DestroyImmediate(gamos[i].gameObject);

					}
				}
				SaveTerrains();
				Initialize();


			}else if(UnityEditor.EditorApplication.isCompiling && CanGenerate == true){
				CanGenerate = false;
				Transform[] gamos = GetComponentsInChildren<Transform>();
				for(int i = 0;i < gamos.Length;i++){
					if(gamos[i]!=transform){
						DestroyImmediate(gamos[i].gameObject);
						for(int t = 0;t < thread.Length;t++){
							if(thread[t]!=null)
								thread[t].Abort();
						}

						CanGenerate = false;
					}
				}
				SaveTerrains();
			}
			}

			if(Camera.current!=null && UnityEditor.Selection.Contains(gameObject)){
				if(timer<=0.25f)
					timer+=0.03f;
				Ray myray ;
				if(playMode== EditorPlay.isEditor){
				myray = UnityEditor.HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
				RaycastHit hit;

				if(Physics.Raycast(myray,out hit,Mathf.Infinity)){
				Shader.SetGlobalVector("_highlightArea",hit.point);
					Event E = Event.current;
					if(E.button==1 && E.control&& timer>=0.25f){
						SetVoxels(hit.point,value,effectArea);
						timer = 0;
					}
					if(E.button==1 && E.alt && timer>=0.25f){
						setMaterial(hit.point,VoxelMaterial,effectArea);
						timer = 0;
					}

					Shader.SetGlobalFloat("_highlightSize",effectArea);}

			}
			}else {
				Shader.SetGlobalFloat("_highlightSize",0);
			}



			//set the wind variables on the shader comment this out if you dont use my grass shader
			if(Zone!=null){
			Shader.SetGlobalFloat("WindAmount",Zone.windMain );
			Shader.SetGlobalFloat("WaveSize" ,Zone.windPulseMagnitude);
			Shader.SetGlobalFloat("windSpeed" , Zone.windPulseFrequency);
			Shader.SetGlobalFloat("SqrDistance", Zone.windTurbulence);
			Shader.SetGlobalFloat("WindDirectionx",Zone.transform.forward.x);
			Shader.SetGlobalFloat("WindDirectiony",Zone.transform.forward.y);
			Shader.SetGlobalFloat("WindDirectionz",Zone.transform.forward.z);

			}

				//if we have meshes to destroy destroy them 
				//need to implement a pooling system will save on memory
				//as well as stop the garbage collector from being called
			if(Trash.Count>0){
				VoxelChunk myChunk = Trash.Dequeue();
				if(myChunk.mesh!=null)
				DestroyImmediate (myChunk.mesh);
				myChunk.mesh=null;
				DestroyImmediate(myChunk.m_mesh);
				myChunk.canDraw=false;
				myChunk.canCreatemesh=false;
			}
			//create the mesh . this is done in update as calling it from another thread is not allowed
			if(MeshChunks.Count>0){
					VoxelChunk chunk = MeshChunks.Dequeue();
					chunk.CreateMesh();
					chunk.canCreatemesh=false;
					if(ActiveChunks.Contains(chunk)==false){
					ActiveChunks.Add(chunk);
				}
			}
			if(ActiveChunks.Count>0){
				for(int i =0;i <ActiveChunks.Count;i++)
					ActiveChunks[i].RenderGrass();
				}
			//need to some how check for occlusion and not render things behind other objects
			



		}
			
void Thread1(){
	while (CanGenerate)
	{

	try
	{	CreateChunks();
		CreateVoxels();
		CreateTris();
		checkActiveChunks();
					Thread.Sleep(1);

		
		

}catch (Exception e){
Debug.LogError(e.StackTrace+e.Message);
}
		}
		}
			
void CreateTris(){
			try
			{	

				if(GenerateVertices.Count >0){
					//Create the voxel data

					//order the chunks before creation so that oonly chunks closest to player get created first
					//had to copy to another array before ordering as it caused errors otherwise
					//use first chunk in the orderered list to create meshes 
					VoxelChunk chunk = GenerateVertices.Dequeue();
					//createmeshes using voxel data
					Createvertices.Enqueue(chunk.CreateVertices);


					//catch all exceptions and display message in console

				}



				CheckVerticesAction();
			}catch (Exception e){
				Debug.LogError(e.StackTrace+e.Message);
			}
		}
void CheckVerticesAction(){
			if(Createvertices.Count>0){
				myAction action = Createvertices.Dequeue();
				action();
			}
			if(EditedActions.Count>0){
				myAction Editaction = EditedActions.Dequeue();
				Editaction();
			}
		}

void checkActiveChunks(){	
				if(ActiveChunks.Count>0){
					for(int i = 0;i < ActiveChunks.Count;i++){
						VoxelChunk chunk = ActiveChunks[i];
					Vector2 chunkpos = new Vector2(chunk.m_pos.x ,chunk.m_pos.z)/TriSize[minLod];

					Vector2 playerpos = Vector2.zero;
					if(Vector2.Distance(chunkpos,playerpos)>=distanceToLoad+25){
							if(chunk.HasChanged && chunk.shouldrender)
								chunk.SaveVoxels();
							Trash.Enqueue(chunk);
						m_voxelChunk.Remove(chunk.RealPos*minLod,new Vector3(m_voxelWidthLength,m_voxelHeight,m_voxelWidthLength));
								ActiveChunks.Remove(chunk);
							return;
						}

					}
				}
		}

void CreateChunks(){
			try
			{
			//basic routine for creating chunks 
				for(int i =0;i<distanceToLoad*m_voxelWidthLength;i+=m_voxelWidthLength){

				//set player positon relative to chunks basically rounding to chunk size

					for(int x=0 ;x < i ;x+=m_voxelWidthLength){if(CanGenerate){

							for(int z=0;z < i;z+=m_voxelWidthLength){if(CanGenerate){

								//this is for the voxel editing at runtime 
								//if theres any new chunks that need to be updated they are added here


								/*if the terrain has been edited calling saveTerrains() method
								will save the voxel data to a file 
								here i just set the flag of this script to cansave =true; and it saves automatically*/
									if(Save){

									SaveTerrains();

										Save = false;
								}
									//set up variables for distance checking
								Vector2 chunkpos = new Vector2(x ,z);

									Vector2 playerpos = Vector2.zero;
								float Dist = Vector2.Distance(playerpos , chunkpos);

									//check if the chunk already exists if not create a chunk with x *y * z of voxels
									if(!m_voxelChunk.ContainsKey(new Vector3(x,0,z),new Vector3(m_voxelWidthLength,m_voxelHeight,m_voxelWidthLength)*minLod)){


										if(Dist <= distanceToLoad){


										Vector3 Pos = new Vector3(x, 0 , z);

										//set variables for chunk creation
										VoxelChunk Chunk = new VoxelChunk(Pos, m_voxelWidthLength, m_voxelHeight ,minLod);



										//add chunk to double key dictionary
											m_voxelChunk.Add(Pos,new Vector3(m_voxelWidthLength,m_voxelHeight,m_voxelWidthLength)*minLod,Chunk) ;

										//set flag on chunk
											Chunk.hasproccessed=true;

										//add chunk to list of chunks that need noise added to them
											GenerateVoxels.Enqueue(Chunk);
											return;

									}
								}

								//if the chunk already exists and its distance is greater then render distance




							}
						}
					}
				}
			}
			}catch (Exception e)
		{Debug.LogError(e.StackTrace);
		}
		}

void CreateVoxels(){

			try
			{	

				if(GenerateVoxels.Count >0){

					//Create the voxel data
					//order the chunks before creation so that oonly chunks closest to player get created first
					//had to copy to another array before ordering as it caused errors otherwise
					//use first chunk in the orderered list to create meshes 
					VoxelChunk chunk = GenerateVoxels.Dequeue();
					//createmeshes using voxel data
					chunk.CreateVoxels();


					//catch all exceptions and display message in console

				}
			}catch (Exception e){
				Debug.LogError(e.StackTrace+e.Message);
			}
		}

	void OnApplicationQuit(){
			//set flag to stop generation
			CanGenerate=false;
			SaveTerrains();
			//abort thread 1

				//abort thread 1
			EndAllThreads();

	}
		void OnDestroy(){
			
			//set flag to stop generation
			CanGenerate=false;
			SaveTerrains();
		}
  }
}
