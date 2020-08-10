using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary; //encription to binary file
using System.IO;
using Newtonsoft.Json;

//I use this class for storing some data about the heightmap in a file -martino
public class GameControl : MonoBehaviour 
{

	public static GameControl control;

	public Dictionary<Vector3, float[,]> storedDictionary;

	//public float[,] currentHeightmap;
	public int currentZoom;
	public double[] currentCenter;
	public Vector3 firstSpawnPoint;
	public List<double> firstBbox;
	public double realGPSSizeX;
	public double realGPSSizeZ;

	void Awake () 
	{
		//I have to attach this script to a game object e.g. empty game object (I may call it controller)
		if (control == null)
		{
			DontDestroyOnLoad(gameObject);
			control = this;
		}
		else if ( control != this){
			Destroy(gameObject);
		}		
	}

	public void SetCurrentHeightmapData (  int _zoom, double[] _centerCoords) {
		//currentHeightmap = _heightmap;
		currentZoom = _zoom;
		currentCenter = _centerCoords;
	}
	public void SetCurrentMapInfo( int _zoom, double[] _centerCoords, Vector3 _firstSpawnPoint, List<double> _firstBbox, double _realGPSSizeX, double _realGPSSizeZ){
		currentZoom = _zoom;
		currentCenter = _centerCoords;
		firstSpawnPoint = _firstSpawnPoint;		
		firstBbox = _firstBbox;
		realGPSSizeX = _realGPSSizeX;
		realGPSSizeZ = _realGPSSizeZ;
	}

	public void SaveCurrentInfo(){
		//usage: GameControl.control.SaveCurrentInfo()
		BinaryFormatter bf = new BinaryFormatter();
		FileStream file = File.Create(Application.persistentDataPath + "/currentMapInfo.dat");
		
		HeightmapData data = new HeightmapData();
		data.currentZoom = currentZoom;
		data.currentCenter = currentCenter;
		data.firstSpawnPoint = new float[3];
		data.firstSpawnPoint[0] = firstSpawnPoint.x;
		data.firstSpawnPoint[1] = firstSpawnPoint.y;
		data.firstSpawnPoint[2] = firstSpawnPoint.z;;
		data.firstBbox = firstBbox;
		data.realGPSSizeX = realGPSSizeX;
		data.realGPSSizeZ = realGPSSizeZ;

		bf.Serialize(file, data);
		file.Close();
		Debug.Log("file created: " + Application.persistentDataPath + "/currentMapInfo.dat");
	}

	public bool LoadCurrentMapInfo(){
		if (File.Exists(Application.persistentDataPath + "/currentMapInfo.dat"))
		{
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Open(Application.persistentDataPath + "/currentMapInfo.dat", FileMode.Open);
			HeightmapData data = (HeightmapData) bf.Deserialize(file);
			file.Close();

			currentZoom = data.currentZoom;
			currentCenter = data.currentCenter;
			firstSpawnPoint = new Vector3(data.firstSpawnPoint[0], data.firstSpawnPoint[1], data.firstSpawnPoint[2] );
			firstBbox = data.firstBbox;
			realGPSSizeX = data.realGPSSizeX;
			realGPSSizeZ = data.realGPSSizeZ;
			Debug.Log("Map info loaded.");
			return true;
		}
		else {
			Debug.Log("no current map info file found");
		}
		return false;
	}



	public void SetCurrentDictionary ( Dictionary<Vector3, float[,] > _dictionary){
		storedDictionary = _dictionary;
	}

	public void SaveDictionary(){

		var jsonSerializerSettings = new JsonSerializerSettings
		{
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore
		};

		var serializeObject = JsonConvert.SerializeObject( new List<KeyValuePair<Vector3, float[,]>>(storedDictionary), jsonSerializerSettings );
		using (FileStream writer = File.Create( Application.persistentDataPath + "/heightmapDictionary.json" ))
		{
			byte[] bytes = Encoding.UTF8.GetBytes( serializeObject );
			writer.Write( bytes, 0, bytes.Length );
		}
		Debug.Log("file created: " + Application.persistentDataPath + "/heightmapDictionary.json");

	}


	public bool LoadDictionary(){
		if (File.Exists(Application.persistentDataPath + "/heightmapDictionary.json"))
		{
			string s = File.ReadAllText( Application.persistentDataPath + "/heightmapDictionary.json" );
			List<KeyValuePair<Vector3, float[,]>> d = JsonConvert.DeserializeObject<List<KeyValuePair<Vector3, float[,]>>>( s );
			storedDictionary = d.ToDictionary( p => p.Key, p => p.Value );
			Debug.Log("Dictionary loaded.");
			return true;
		}
		else {
			Debug.Log("Dictionary file doesn't exist in expected path: " + Application.persistentDataPath + "/heightmapDictionary.json");
		}
		return false;
	}

	public void SaveAsCurrent()
	{
		//usage: GameControl.control.SaveAsCurrent()
		//BinaryFormatter bf = new BinaryFormatter();
		//FileStream file = File.Create(Application.persistentDataPath + "/currentHeightsInfo.dat");
		
		//HeightmapData data = new HeightmapData();
		//data.heightmap = currentHeightmap;
		//data.zoomLevel = currentZoom;
		//data.centerCoords = currentCenter;

		//bf.Serialize(file, data);
		//file.Close();
		//Debug.Log("file created: " + Application.persistentDataPath + "/currentHeightsInfo.dat");
	}

	public bool LoadCurrent()
	{
		if (File.Exists(Application.persistentDataPath + "/currentHeightsInfo.dat"))
		{
			//BinaryFormatter bf = new BinaryFormatter();
			//FileStream file = File.Open(Application.persistentDataPath + "/currentHeightsInfo.dat", FileMode.Open);
			//HeightmapData data = (HeightmapData) bf.Deserialize(file);
			//file.Close();
			//currentHeightmap = data.heightmap;
			//currentZoom = data.zoomLevel;
			//currentCenter = data.centerCoords;
			return true;
		} 
		else {
			Debug.Log("no current map info file found");
		}
		return false;
	}

	public bool checkCurrent() {
		return File.Exists(Application.persistentDataPath + "/currentHeightsInfo.dat");
	}

}

[Serializable]
class HeightmapData	
{
	//public float[,] heightmap;
	//public int zoomLevel;
	//public double[] centerCoords;
	public int currentZoom;
	public double[] currentCenter;
	public float[] firstSpawnPoint;
	public List<double> firstBbox;
	public double realGPSSizeX;
	public double realGPSSizeZ;
}