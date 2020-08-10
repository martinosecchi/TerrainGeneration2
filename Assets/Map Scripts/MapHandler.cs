using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using RestSharp;


[System.Serializable]
public class BingMapsLocation
{
	public string locality = "trento";
	public string adminDistrict = "";
	public string addressLine = "";
	public string ISOCountryRegion = "IT";
	public int zoom = 12;
	public double latitude;
	public double longitude;
}

public class MapHandler : MonoBehaviour {
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
	public 	int smoothings = 3; //times I do the smoothing
	public int neighbors = 12; //neighbors I want to use for smoothing height values

	private string _key = "secret";
	private readonly int _size = 512; 
	private List<double> _centerCoord = new List<double>(2);
	private List<double> _lastCenter = new List<double>(2);
	private List<double> _firstBbox = new List<double>(4);
	private double _realGPSSizeX;
	private double _realGPSSizeZ;
	private Vector3 _firstSpawnPoint;
	private bool _firstRequest = true;
	private readonly Comparer _vectorComparer = new Comparer();

	protected Dictionary<Vector3, float[,]> _cachedHeightmaps = new Dictionary<Vector3, float[,] >();

	public void StartMap(Vector3 cellSize, Vector3 center){	
		if (_firstRequest){
			Initialize( cellSize, center);
			_firstRequest = false;
			if ( allowLoad && GameControl.control.LoadDictionary()) {
				_cachedHeightmaps = GameControl.control.storedDictionary;
			}
		}
		if ( !_cachedHeightmaps.Keys.Contains( center, _vectorComparer )){
			saveCachedHeightmaps(cellSize, center);
		}
	}

	public void Initialize( Vector3 cellSize, Vector3 center){
		GetGeneralRequest(); //initialize _centerCoord through geolocation
		if (allowLoad && GameControl.control.LoadCurrentMapInfo()
			&& GameControl.control.currentCenter[0] == _centerCoord[0]
			&& GameControl.control.currentCenter[1] == _centerCoord[1]
			&& GameControl.control.currentZoom == centerLocation.zoom )
		{
			_firstBbox = GameControl.control.firstBbox;
			_firstSpawnPoint = GameControl.control.firstSpawnPoint;
			_realGPSSizeX = GameControl.control.realGPSSizeX;
			_realGPSSizeZ = GameControl.control.realGPSSizeZ;
			Debug.Log ("map info initialized from file");
		}
		else {
			MetadataObject metadata = _RequestMetadata( GetGeneralRequest() + "&mapMetadata=1" );
			//_firstSpawnPoint = new Vector3(center.x + cellSize.x, center.y, center.z + cellSize.z);
			_firstSpawnPoint = new Vector3(center.x, center.y, center.z);
			_firstBbox = metadata.resourceSets[0].resources[0].bbox;
			//Debug.Log("init bbox: " + _firstBbox[0] + "," + _firstBbox[1] + "," + _firstBbox[2] + "," + _firstBbox[3]);
			_realGPSSizeX = _firstBbox[3] - _firstBbox[1]; //east - west
			_realGPSSizeZ = _firstBbox[2] - _firstBbox[0]; //north - south
			GameControl.control.SetCurrentMapInfo(centerLocation.zoom, _centerCoord.ToArray(), _firstSpawnPoint, _firstBbox, _realGPSSizeX, _realGPSSizeZ);
			GameControl.control.SaveCurrentInfo();
		}
	}

	public void saveCachedHeightmaps( Vector3 cellSize, Vector3 center ){
		string qs = "/" + GetCenterPoint( cellSize, center) + "/" + centerLocation.zoom + "?";
		//qs += "mapArea=" + GetBBoxString(cellSize, center);
		qs += "&mapSize=" + _size + "," + _size;
		qs += "&key=" + _key + "&mapMetadata=1";

		MetadataObject metadata = _RequestMetadata(qs);
		List<double> bbox = metadata.resourceSets[0].resources[0].bbox;

		int rows = 32; 		//max number of heights retrievable = 1024 = 32 * 32
		qs = "?bounds=";
		qs += bbox[0] + "," + bbox[1] + "," +  bbox[2] + "," +  bbox[3];
		qs += "&rows=" + rows + "&cols=" + rows + "&heights=sealevel&key=" + _key;

		ElevDataObject elevData = _RequestElevations( qs );
		List<int> elevations = elevData.resourceSets[0].resources[0].elevations;  //elevations at sea level in meters
		
		int width = 512 * 3 ;
		float[,] heightmapData = new float[width, width]; //terrain.GetHeights(0, 0, width, width);

		if (width % rows == 0)
		{	
			heightmapData = ApplyElevationsToHeightmap (elevations, heightmapData, width);
			heightmapData = Smooth(heightmapData, smoothings, neighbors, width);
			for (int i = 0; i < 9; i++){
				if (!_cachedHeightmaps.Keys.Contains( GetCachedHMIndex2(center, cellSize, i) ))
				_cachedHeightmaps.Add( GetCachedHMIndex2(center, cellSize, i) , GetHeightmapForChunk(heightmapData, i));
			}
			//GameControl.control.SetCurrentDictionary( _cachedHeightmaps );
		}
		else {
			Debug.Log ("Something went wrong: size of terrain is not processable for heightmap generation");
		}
	}

	private MetadataObject _RequestMetadata( string qs ){
		RestClient _client = new RestClient();
		_client.BaseUrl = new System.Uri( GetBaseImageryUrl() );
		var request = new RestRequest();
		request.Resource = qs ;
		request.RequestFormat = DataFormat.Json;
		request.JsonSerializer = new RestSharpJsonNetSerializer();
		var response = _client.Get<MetadataObject>(request);

		//Debug.Log("metadata request: " + _client.BaseUrl + request.Resource);

		if (response.Data.statusDescription == "OK")
		{
			_lastCenter[0] = double.Parse(response.Data.resourceSets[0].resources[0].mapCenter.coordinates[0]);
			_lastCenter[1] = double.Parse(response.Data.resourceSets[0].resources[0].mapCenter.coordinates[1]);
			return response.Data;
		}
		Debug.Log("Bad Request: Metadata -> " + _client.BaseUrl + request.Resource );
		return new MetadataObject();
	}

	private ElevDataObject _RequestElevations( string qs ){
		string url = "http://dev.virtualearth.net/REST/v1/Elevation/Bounds";
		RestClient _client = new RestClient();
		_client.BaseUrl = new System.Uri(url);
		RestRequest request = new RestRequest();
		request.Resource = qs;
		request.RequestFormat = DataFormat.Json;
		request.JsonSerializer = new RestSharpJsonNetSerializer();
		var response = _client.Get<ElevDataObject>(request);

		//Debug.Log(url + "" + qs);

		if (response.Data.statusDescription == "OK")
		return response.Data;
		Debug.Log("Bad Request: Metadata -> " + request);
		return new ElevDataObject();
	}

	public Texture2D GetTexture( Vector3 cellSize, Vector3 center){
		string qs = "/" + GetCenterPointForTexture( cellSize, center) + "/" + centerLocation.zoom + "?";
		//qs += "mapArea=" + GetBBoxStringForTexture(cellSize, center);
		qs += "&mapSize=" + _size + "," + _size;
		qs += "&key=" + _key + "&mapMetadata=0";

		RestClient _client = new RestClient();
		_client.BaseUrl = new System.Uri( GetBaseImageryUrl() );
		var request = new RestRequest();
		request.Resource = qs ;
		IRestResponse response = _client.Execute(request);

		//Debug.Log ("texture request: " +  _client.BaseUrl + request.Resource );
		Texture2D tex = new Texture2D( 2, 2); //random, will resize from LoadImage
		tex.LoadImage(response.RawBytes);
		return tex;
	}

	public TerrainData ApplyTextureToTerrainData( TerrainData terrainData, Vector3 cellSize, Vector3 center ){
		List<SplatPrototype> splatList = new List<SplatPrototype>();
		SplatPrototype newSplat = new SplatPrototype();
		
		newSplat.texture = GetTexture(cellSize, center);
		
		newSplat.tileSize = new Vector2( cellSize.x, cellSize.x );
		newSplat.tileOffset = Vector2.zero;
		splatList.Add (newSplat);
		terrainData.splatPrototypes = splatList.ToArray();

		return terrainData;
	}

	public TerrainData ApplyTextureToTerrainData2( TerrainData terrainData, Vector3 cellSize, Vector3 center){
		List<SplatPrototype> splatList = new List<SplatPrototype>();
		SplatPrototype newSplat = new SplatPrototype();
		
		newSplat.texture = GetTexture2(cellSize, center);
		
		newSplat.tileSize = new Vector2( cellSize.x, cellSize.x );
		newSplat.tileOffset = Vector2.zero;
		splatList.Add (newSplat);
		terrainData.splatPrototypes = splatList.ToArray();

		return terrainData;
	}

	public Texture2D GetTexture2( Vector3 cellSize, Vector3 center){

		int width = 256; //max resolution from bing is 1500x1500, not enough for a 512px width

		string qs = "/" ;
		qs +=  _lastCenter[0] + "," + _lastCenter[1] + "/" + centerLocation.zoom + "?";
		qs += "&mapSize=" + (width*3)+ "," + (width*3);
		qs += "&key=" + _key + "&mapMetadata=0";

		RestClient _client = new RestClient();
		_client.BaseUrl = new System.Uri( GetBaseImageryUrl() );
		var request = new RestRequest();
		request.Resource = qs ;
		IRestResponse response = _client.Execute(request);

		//Debug.Log ("texture request: " +  _client.BaseUrl + request.Resource );
		Texture2D tex = new Texture2D( 2, 2); //random, will resize from LoadImage -> width * 3
		tex.LoadImage(response.RawBytes);

		Texture2D destinationTex = new Texture2D ( width, width);
		Color[] destinationPixels = destinationTex.GetPixels();

		int xOffset = (int) ( (center.x - _firstSpawnPoint.x ) / cellSize.x) - 1; //how many cells away on x-axis ( + offset for starting at lower corner and not at center)
		int zOffset = (int) ( (center.z - _firstSpawnPoint.z ) / cellSize.z) - 1;
		Vector2 vectIndex = GetIndexTextureChunk2(xOffset, zOffset, width);
		
		int xStart = (int) vectIndex.x;
		int yStart = (int) vectIndex.y;

		destinationPixels = tex.GetPixels( xStart, yStart, width, width );
		destinationTex.SetPixels(destinationPixels);
		destinationTex.Apply();

		return destinationTex ;
	}

	public Vector2 GetIndexTextureChunk2( int xOffset, int zOffset, int width ){
		Vector2 res = new Vector2(0,0);
		
		res = GetIndexTextureChunk(xOffset % 2, zOffset % 2, width);

		return res;
	}

	public Vector2 GetIndexTextureChunk( int xOffset, int zOffset, int width ){
		Vector2 res = new Vector2(0,0);
		if (xOffset == 0)
		{
			res.x = width;
		}
		else if (xOffset == 1){
			res.x = 2*width;
		}
		else if (xOffset == -1){
			res.x = 0;
		}
		if (zOffset == 0)
		{
			res.y = width;
		}
		else if (zOffset == 1){
			res.y = 2*width;
		}
		else if (zOffset == -1){
			res.y = 0;
		}
		return res;
	}

	public string GetCenterPoint(Vector3 cellSize, Vector3 center){
		string ret = "";
		int xOffset = (int) ( (center.x - _firstSpawnPoint.x ) / cellSize.x); //how many cells away on x-axis
		int zOffset = (int) ( (center.z - _firstSpawnPoint.z ) / cellSize.z);
		ret += ( _centerCoord[0] + (_realGPSSizeZ / 3 * zOffset)) + ","; //lat -> how north/south
		ret += ( _centerCoord[1] + (_realGPSSizeX / 3 * xOffset)); //lng -> how east/west
		return ret;
	}

	public string GetCenterPointForTexture(Vector3 cellSize, Vector3 center){
		string ret = "";
		int xOffset = (int) ( (center.x - _firstSpawnPoint.x ) / cellSize.x) - 1; //how many cells away on x-axis ( + offset for starting at lower corner and not at center)
		int zOffset = (int) ( (center.z - _firstSpawnPoint.z ) / cellSize.z) - 1;

		ret += ( (_centerCoord[0] + _realGPSSizeZ/3) + (_realGPSSizeZ * zOffset)) + ","; //lat -> how north/south
		ret += ( (_centerCoord[1] + _realGPSSizeX/3) + (_realGPSSizeX * xOffset)); //lng -> how east/west
		return ret;
	}
	
	public string GetUrlMetadata(){
		return GetBaseImageryUrl() + GetGeneralRequest() + "&mapMetadata=1";
	}

	public string GetUrlImage(){
		return GetBaseImageryUrl() + GetGeneralRequest() + "&mapMetadata=0";
	}

	public string GetBaseLocationUrl(){
		return "http://dev.virtualearth.net/REST/v1/Locations";
	}

	public string GetBaseImageryUrl(){
		return "http://dev.virtualearth.net/REST/v1/Imagery/Map/" + imSet;
	}

	public string GetGeneralRequest(){
		string qs = "";
		qs +=  (centerLocation.locality!= "") ? "locality=" + centerLocation.locality : "";
		qs +=  (centerLocation.adminDistrict!= "") ? "&adminDistrict=" + centerLocation.adminDistrict : "";
		qs +=  (centerLocation.addressLine!= "") ? "&addressLine=" + centerLocation.addressLine : "";
		qs +=  (centerLocation.ISOCountryRegion!= "") ? "&countryRegion=" + centerLocation.ISOCountryRegion : "";

		if (_centerCoord.Count () == 0) 
		{
			if (qs != "") 
			{
				GeolocateAddress (qs);
			}
			else {
				if (centerLocation.latitude != 0 && centerLocation.longitude != 0)
				{
					_centerCoord.Add (centerLocation.latitude);
					_centerCoord.Add (centerLocation.longitude);			
				} 
				else {
					Debug.Log ("Something went wrong: no valid coordinates found");
				}
			}
		}
		Debug.Assert (_centerCoord.Count() == 2);
		_lastCenter = _centerCoord;
		qs = "/";
		qs += _centerCoord[0] + "," + _centerCoord[1] + "/";
		qs += centerLocation.zoom + "?";
		qs += "mapSize=" + _size + "," + _size;
		qs += "&key=" + _key;
		return qs;
	}
	public void GeolocateAddress( string qs){
		RestClient _client = new RestClient();
		_client.BaseUrl = new System.Uri( GetBaseLocationUrl() );
		var request = new RestRequest();
		request.Resource = "?" + qs + "&maxResults=1" + "&key=" + _key;
		request.RequestFormat = DataFormat.Json;
		request.JsonSerializer = new RestSharpJsonNetSerializer();
		var response = _client.Get<GeocodedObject>(request);
		if (response.Data.statusDescription == "OK")
		{
			_centerCoord = response.Data.resourceSets[0].resources[0].point.coordinates;
		}
		else {
			Debug.Log("Something went wrong: no location retrieved from geolocation");
		}
	}

	public float[,] GetHeightmapForChunk ( float[,] heightmapData , int chunk ){
		int width = 512 ;

		Vector3 index = GetIndex(chunk , 3, width * 3);

		float[,] heightmap = new float[width, width];
		int m = 0, n = 0;
		//take only the section needed from the big heightmap
		for (int x = (int) index.x ; x < index.x + index.z ; x++){
			for (int y = (int) index.y; y < index.y + index.z ; y++ ){
				heightmap[m,n] = heightmapData[x,y];
				n++;
			}
			n = 0;
			m++;
		}
		return heightmap;
	}


	public Vector3 GetIndex(int i, int gridWidth, int width ){
		// chunks are numbered 0..8
		if ( width % gridWidth == 0)
		{
			//x and y are the offsets in the heightmapData for the required chunk, z is the width in pixels of each chunk
			Vector3 position = new Vector3(0,0,0);
			int index = width / gridWidth;
			switch(i){
				case 0:
				position = new Vector3(0,0, index);
				break;
				case 3://1
				position = new Vector3(index,0, index);
				break;
				case 6: //2
				position = new Vector3(2*index,0, index);
				break;
				case 1: //3
				position = new Vector3(0,index, index);
				break;
				case 4:
				position = new Vector3(index,index, index);
				break;
				case 7: //5
				position = new Vector3(2*index,index, index);
				break;
				case 2: //6
				position = new Vector3(0,2*index, index);
				break;
				case 5: //7
				position = new Vector3(index,2*index, index);
				break;
				case 8:
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

	public Vector3 GetCachedHMIndex2(Vector3 center, Vector3 cellSize, int i){
		//starting with 0 as lower left box
		switch(i){
			case 0:
			return new Vector3(center.x, center.y, center.z );
			case 1:
			return new Vector3(center.x + cellSize.x, center.y, center.z);
			case 2:
			return new Vector3(center.x + (2*cellSize.x), center.y, center.z);
			case 3:
			return new Vector3(center.x, center.y, center.z + cellSize.z );
			case 4:
			return new Vector3(center.x + cellSize.x, center.y, center.z + cellSize.z);
			case 5:
			return new Vector3(center.x + (2*cellSize.x), center.y, center.z + cellSize.z);
			case 6:
			return new Vector3(center.x, center.y, center.z + (2*cellSize.z) );
			case 7:
			return new Vector3(center.x + cellSize.x, center.y, center.z + (2*cellSize.z) );
			case 8:
			return new Vector3(center.x + (2*cellSize.x), center.y, center.z + (2*cellSize.z) );
			default: 
			Debug.Log("Something went wrong: impossible chunk");
			return new Vector3();
		}
	}

	public float[,] ApplyElevationsToHeightmap( List<int> elevations, float[,] heightmapData, int width ){
		float minELev = Mathf.Min(elevations.ToArray());//meters
		float maxElev = Mathf.Max(elevations.ToArray());//meters

		if (minELev <= 0)
			minELev = 1; //so we don't divide by 0

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

	public float[,] Smooth( float[,] heightmapData , int smoothings, int neighbors, int width ) {
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

	void OnDestroy(){
		//GameControl.control.SaveDictionary();
	}
}



// -------- CLASSES FOR JSON PARSING --------
//for geolocation


public class Point
{
	public string type { get; set; }
	public List<double> coordinates { get; set; }
}

public class Address
{
	public string adminDistrict { get; set; }
	public string adminDistrict2 { get; set; }
	public string countryRegion { get; set; }
	public string formattedAddress { get; set; }
	public string locality { get; set; }
}

public class GeocodePoint
{
	public string type { get; set; }
	public List<double> coordinates { get; set; }
	public string calculationMethod { get; set; }
	public List<string> usageTypes { get; set; }
}

public class Resource
{
	public string __type { get; set; }
	public List<double> bbox { get; set; }
	public string name { get; set; }
	public Point point { get; set; }
	public Address address { get; set; }
	public string confidence { get; set; }
	public string entityType { get; set; }
	public List<GeocodePoint> geocodePoints { get; set; }
	public List<string> matchCodes { get; set; }
}

public class ResourceSet
{
	public int estimatedTotal { get; set; }
	public List<Resource> resources { get; set; }
}

public class GeocodedObject
{
	public string authenticationResultCode { get; set; }
	public string brandLogoUri { get; set; }
	public string copyright { get; set; }
	public List<ResourceSet> resourceSets { get; set; }
	public int statusCode { get; set; }
	public string statusDescription { get; set; }
	public string traceId { get; set; }
}

// for map metadata

class MapCenter
{
	public string type { get; set; }
	public List<string> coordinates { get; set; }
}

class ResourceMeta
{
	public string __type { get; set; }
	public List<double> bbox { get; set; }
	public string imageHeight { get; set; }
	public string imageWidth { get; set; }
	public MapCenter mapCenter { get; set; }
	public List<object> pushpins { get; set; }
	public string zoom { get; set; }
}

class ResourceMetaSet
{
	public int estimatedTotal { get; set; }
	public List<ResourceMeta> resources { get; set; }
}

class MetadataObject
{
	public string authenticationResultCode { get; set; }
	public string brandLogoUri { get; set; }
	public string copyright { get; set; }
	public List<ResourceMetaSet> resourceSets { get; set; }
	public int statusCode { get; set; }
	public string statusDescription { get; set; }
	public string traceId { get; set; }
}

//for elevation data

class ResourceElev
{
	public string __type { get; set; }
	public List<int> elevations { get; set; }
	public int zoomLevel { get; set; }
}

class ResourceElevSet
{
	public int estimatedTotal { get; set; }
	public List<ResourceElev> resources { get; set; }
}

class ElevDataObject
{
	public string authenticationResultCode { get; set; }
	public string brandLogoUri { get; set; }
	public string copyright { get; set; }
	public List<ResourceElevSet> resourceSets { get; set; }
	public int statusCode { get; set; }
	public string statusDescription { get; set; }
	public string traceId { get; set; }
}
