using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

public class BingMapsTerrain : MonoBehaviour {
	public enum ImagerySet {
		Aerial,
		AerialWithLabels,
		Birdseye,
		BirdseyeWithLabels,
		Road
	}
	public bool loadOnStart = true;
	public ImagerySet imSet;
	public BingMapsLocation centerLocation;
	public bool heights = true;
	public bool allowLoad = true;
	public 	int smoothings = 4; //times I do the smoothing
	public int neighbors = 6; //neighbors I want to use for smoothing height values

	
	void Start() {
		if(loadOnStart) Refresh();	
	}
	
	public void Refresh() {
		StartCoroutine(_Refresh());
	}
	
	IEnumerator _Refresh (){
		string key = "secret";
		int size = 512; 
		GetComponent<Terrain> ().terrainData.size = new Vector3 (25, 10, 25);
		GetComponent<Terrain> ().terrainData.heightmapResolution = size;

		List<double> centerCoord = new List<double>(2);
		string url = "http://dev.virtualearth.net/REST/v1/Locations?"; 
		string qs = "";
		qs +=  (centerLocation.locality!= "") ? "locality=" + centerLocation.locality : "";
		qs +=  (centerLocation.adminDistrict!= "") ? "&adminDistrict=" + centerLocation.adminDistrict : "";
		qs +=  (centerLocation.addressLine!= "") ? "&addressLine=" + centerLocation.addressLine : "";
		qs +=  (centerLocation.ISOCountryRegion!= "") ? "&countryRegion=" + centerLocation.ISOCountryRegion : "";

		if (qs != "")
		{
			WWW geocode = new WWW (url + qs + "&maxResults=1&key=" + key);
			yield return geocode;
			GeocodedObject locationJson = JsonConvert.DeserializeObject<GeocodedObject>( geocode.text );
			if (locationJson.statusDescription == "OK")
			{
				centerCoord = locationJson.resourceSets[0].resources[0].point.coordinates;
			}
			else {
				Debug.Log("Something went wrong: no location retrieved from geolocation");
			}
		}
		else {
			if (centerLocation.latitude != 0 && centerLocation.longitude != 0)
			{
				centerCoord[0] = centerLocation.latitude;
				centerCoord[1] = centerLocation.longitude;				
			}
			else {
				Debug.Log("Something went wrong: no valid coordinates found");
			}
		}
		//set up the request
		url = "http://dev.virtualearth.net/REST/v1/Imagery/Map/";
		qs = "";
		qs += imSet + "/";
		qs += centerCoord[0] + "," + centerCoord[1] + "/";
		qs += centerLocation.zoom + "?";
		qs += "mapSize=" + size + "," + size;
		qs += "&key=" + key;

		string qs2 = qs;
		qs += "&mapMetadata=0"; //for having the image
		qs2 += "&mapMetadata=1"; //for map metadata

		WWW reqImage = new WWW (url + qs);
		if (heights == true ){
			if (allowLoad && GameControl.control.LoadCurrent() 
				&& GameControl.control.currentCenter[0] == centerCoord[0]
				&& GameControl.control.currentCenter[1] == centerCoord[1]
				&& GameControl.control.currentZoom == centerLocation.zoom) {
				Debug.Log("heightmap loaded");
				//GetComponent<Terrain> ().terrainData.SetHeights(0,0, GameControl.control.currentHeightmap);
				//ApplyBingHeightmapToChunks (GameControl.control.currentHeightmap, 9);
			} 
			else {
				WWW reqMeta = new WWW (url + qs2);
				yield return reqMeta;
				StartCoroutine(ApplyBingHeightmapV2(reqMeta, key));
			}
		}

		yield return reqImage;

		//need this to add the texture to the terrain so it looks like a map
		List<SplatPrototype> splatList = new List<SplatPrototype>();
		SplatPrototype newSplat = new SplatPrototype();
		newSplat.texture = reqImage.texture; //satellite: req, heightmap: req2 (use req normally)
		float width = GetComponent<Terrain> ().terrainData.size.x;
		newSplat.tileSize = new Vector2( width, width );
		newSplat.tileOffset = Vector2.zero;
		splatList.Add (newSplat);
		GetComponent<Terrain> ().terrainData.splatPrototypes = splatList.ToArray();
	}


	void ApplyBingHeightmapToChunks( float[,] heightmapData, int chunks ){

		//TerrainData terrain = GetComponent<Terrain> ().terrainData;
		int width = 512 ;

		GameObject[] terrainGOs = new GameObject[chunks];
		TerrainData[] tDatas = new TerrainData[chunks];
		int gridWidth = (int) Mathf.Sqrt(chunks); //let's pretend it is a good value
		int cellWidth = 25;
		Vector3 cellSize = new Vector3(cellWidth, cellWidth, cellWidth);

		for(int i = 0; i < chunks; i++){
			Vector3 position = GetPosition(i+1, cellWidth);
			Vector3 index = GetIndex(i+1, gridWidth, width * 3);
			float[,] heightmap = new float[width, width];
			int m = 0, n = 0;
			for (int x = (int) index.x ; x < index.x - 1 + index.z ; x++){
				for (int y = (int) index.y; y < index.y - 1 + index.z ; y++ ){
					heightmap[m,n] = heightmapData[x,y];
					n++;
					//n+= gridWidth;
				}
				n = 0;
				m++;
				//m+= gridWidth;
			}

			tDatas[i] = new TerrainData(); 
			tDatas[i].heightmapResolution = (width+1); // /gridWidth;
			tDatas[i].size = cellSize;
			tDatas[i].SetHeights(0, 0, heightmap);
			Debug.Log(" heightmapSize: " + heightmap.GetLength(0) + " big one is: " + width * 3);

			terrainGOs[i] = Terrain.CreateTerrainGameObject(tDatas[i]);
			terrainGOs[i].GetComponent<Terrain>().terrainData.size = cellSize;

			terrainGOs[i].transform.position = position;
			terrainGOs[i].GetComponent<Terrain>().Flush();
		}
	}


	Vector3 GetIndex(int i, int gridWidth, int width ){
		if ( width % gridWidth == 0)
		{
			//x and y are the offsets in the heightmapData for the required chunk, z is the width in pixels of each chunk
			Vector3 position = new Vector3(0,0,0);
			int index = width / gridWidth;
			switch(i){
				case 1:
				position = new Vector3(0,0, index);
				break;
				case 4://2
				position = new Vector3(index,0, index);
				break;
				case 7: //3
				position = new Vector3(2*index,0, index);
				break;
				case 2: //4
				position = new Vector3(0,index, index);
				break;
				case 5:
				position = new Vector3(index,index, index);
				break;
				case 8: //6
				position = new Vector3(2*index,index, index);
				break;
				case 3: //7
				position = new Vector3(0,2*index, index);
				break;
				case 6: //8
				position = new Vector3(index,2*index, index);
				break;
				case 9:
				position = new Vector3(2*index,2*index, index);
				break;
			}
			return position;
		}
		else {
			Debug.Log("Something went wrong: resolution not valid for this number of chunks");
			Debug.Log(width + "%" + gridWidth + " = " + width%gridWidth);
			return new Vector3();
		}
	}

	Vector3 GetPosition(int i, int cellWidth ){
		Vector3 position = new Vector3();
		switch(i){
			case 1:
			position = new Vector3(0,0,0);
			break;
			case 2:
			position = new Vector3(cellWidth,0,0);
			break;
			case 3:
			position = new Vector3(2*cellWidth,0,0);
			break;
			case 4:
			position = new Vector3(0,0,cellWidth);
			break;
			case 5:
			position = new Vector3(cellWidth,0,cellWidth);
			break;
			case 6:
			position = new Vector3(2*cellWidth,0,cellWidth);
			break;
			case 7:
			position = new Vector3(0,0,2*cellWidth);
			break;
			case 8:
			position = new Vector3(cellWidth,0,2*cellWidth);
			break;
			case 9:
			position = new Vector3(2*cellWidth,0,2*cellWidth);
			break;
		}
		return position;
	}

	IEnumerator ApplyBingHeightmapV2(WWW metaReq, string key ){

		MetadataObject metadata = JsonConvert.DeserializeObject<MetadataObject>( metaReq.text );
		List<double> bbox = metadata.resourceSets[0].resources[0].bbox;

		string url = "http://dev.virtualearth.net/REST/v1/Elevation/Bounds?bounds=";
		url += bbox[0] + "," + bbox[1] + "," +  bbox[2] + "," +  bbox[3];
		int rows = 32;
		url += "&rows=" + rows + "&cols=" + rows + "&heights=sealevel&key=" + key;
		//max height point retrievable = 1024 = 32 * 32

		WWW elevReq = new WWW (url);
		yield return elevReq;

		ElevDataObject elevData = JsonConvert.DeserializeObject<ElevDataObject> (elevReq.text);
		List<int> elevations = elevData.resourceSets[0].resources[0].elevations;  //elevations at sea level in meters
		

		TerrainData terrain = GetComponent<Terrain> ().terrainData;
		int width = (terrain.heightmapWidth  - 1) * 3 ;
		float[,] heightmapData = new float[width, width]; //terrain.GetHeights(0, 0, width, width);

		if (width % rows == 0)
		{	
			heightmapData = ApplyElevationsToHeightmap (elevations, heightmapData, width);
			heightmapData = Smooth(heightmapData, smoothings*2, neighbors*2, width);

			double[] coordinates = new double[2];
			coordinates[0] = double.Parse(metadata.resourceSets[0].resources[0].mapCenter.coordinates[0]);
			coordinates[1] = double.Parse(metadata.resourceSets[0].resources[0].mapCenter.coordinates[1]);
			//GameControl.control.SetCurrentHeightmapData(heightmapData, centerLocation.zoom, coordinates);
			//GameControl.control.SaveAsCurrent();
		}
		else {
			Debug.Log ("Something went wrong: size of terrain is not processable for heightmap generation");
		}
		
	}
		IEnumerator ApplyBingHeightmapV1(WWW metaReq, string key ){

		MetadataObject metadata = JsonConvert.DeserializeObject<MetadataObject>( metaReq.text );
		List<double> bbox = metadata.resourceSets[0].resources[0].bbox;

		string url = "http://dev.virtualearth.net/REST/v1/Elevation/Bounds?bounds=";
		url += bbox[0] + "," + bbox[1] + "," +  bbox[2] + "," +  bbox[3];
		int rows = 32;
		url += "&rows=" + rows + "&cols=" + rows + "&heights=sealevel&key=" + key;
		//max height point retrievable = 1024 = 32 * 32

		WWW elevReq = new WWW (url);
		yield return elevReq;

		ElevDataObject elevData = JsonConvert.DeserializeObject<ElevDataObject> (elevReq.text);
		List<int> elevations = elevData.resourceSets[0].resources[0].elevations;  //elevations at sea level in meters
		

		TerrainData terrain = GetComponent<Terrain> ().terrainData;
		int width = terrain.heightmapWidth-1;
		float[,] heightmapData = terrain.GetHeights(0, 0, width, width);
		

		if (width % rows == 0)
		{	
			heightmapData = ApplyElevationsToHeightmap (elevations, heightmapData, width);
			heightmapData = Smooth(heightmapData, smoothings, neighbors, width);

			double[] coordinates = new double[2];
			coordinates[0] = double.Parse(metadata.resourceSets[0].resources[0].mapCenter.coordinates[0]);
			coordinates[1] = double.Parse(metadata.resourceSets[0].resources[0].mapCenter.coordinates[1]);
			//GameControl.control.SetCurrentHeightmapData(heightmapData, centerLocation.zoom, coordinates);
			//GameControl.control.SaveAsCurrent();
			terrain.SetHeights(0, 0, heightmapData);
		}
		else {
			Debug.Log ("Something went wrong: size of terrain is not processable for heightmap generation");
		}
	}

	float[,] ApplyElevationsToHeightmap( List<int> elevations, float[,] heightmapData, int width ){
		float minELev = Mathf.Min(elevations.ToArray());//meters
		float maxElev = Mathf.Max(elevations.ToArray());//meters
		float maxReach = 8850; //meters -> m. everest
		int index = 0;
		int rows = 32;
		float zoomScale = (float) ((centerLocation.zoom - 1.0)/(21.0 - 1.0)) * (maxElev - minELev)/minELev; //21 is max zoom value
		
		for (int y = 0; y < width; y+=width/rows) {
			for (int x = 0; x < width; x+=width/rows) {
				if (y % (width/rows) == 0 && x % (width/rows) == 0) 
				{
					index = (y*rows + x )/(width/rows);
					heightmapData[y, x] = ((elevations[index] - minELev)/(maxReach - minELev)) * zoomScale; 
				}
				else {
					heightmapData[y, x] = minELev; //initialize empty with min elevation
				}
			}
		}
		float distance = 0;
		// (x, y) is a peak, adjust the other values (weighted avg according to distance to peak)
		for (int y=0; y < width; y+=width/rows){
			for (int x=0; x < width; x+=width/rows){
				// each peak works on his square [x+n, y+m]
				for (int n = 0; n < width/rows; n ++){
					for (int m = 0; m < width/rows; m++){
						if ((y+m) % (width/rows) != 0 || (x+n) % (width/rows) != 0) {
							distance = Mathf.Sqrt((m*m) + (n*n));
							heightmapData[y+m, x+n] = heightmapData[y,x] * (1 - (distance/((Mathf.Sqrt(2) * width/rows))));
						}
					}
				}
			}
		}
		return heightmapData;
	}

	float[,] Smooth( float[,] heightmapData , int smoothings, int neighbors, int width ) {
		float sum =0;
		int count = 0;
		//smooth everything
		for (int i=0; i < smoothings; i++){
			for (int y=0; y < width; y++){
				for (int x=0; x < width; x++){
					for (int n = 0; n < neighbors; n++){
						sum += (y-n <= 0 ? 0 : heightmapData[y-n, x]);
						sum += (x-n <= 0 ? 0 : heightmapData[y, x-n]);
						sum += ((x-n<=0 || y-n<=0) ? 0 : heightmapData[y-n, x-n]);
						count += (y-n <= 0 ? 0 : 1);
						count += (x-n <= 0 ? 0 : 1);
						count += ((x-n<=0 || y-n<=0) ? 0 : 1);
					}
					heightmapData[y,x] = (heightmapData[y,x] + sum )/ (count+1);
					sum = 0;
					count = 0;
				}
			}
		}
		return heightmapData;
	}
}
