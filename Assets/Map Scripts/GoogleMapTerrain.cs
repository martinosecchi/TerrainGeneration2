using UnityEngine;
using System.Collections;
using System.Collections.Generic;


[System.Serializable]
public class GoogleMapLocation
{
	public string address = "molveno";
	public float latitude;
	public float longitude;
}

public class GoogleMapTerrain : MonoBehaviour {
	public enum MapType {
		Satellite,
		RoadMap,
		Hybrid,
		Terrain
	}
	public bool loadOnStart = true;
	public GoogleMapLocation centerLocation;
	public int zoom = 12;
	public MapType mapType;
	public bool doubleResolution = false;
	public int lightness = -100;
	public string style = "";
	
	void Start() {
		if(loadOnStart) Refresh();	
	}
	
	public void Refresh() {
		StartCoroutine(_Refresh());
	}
	
	IEnumerator _Refresh (){
		//resize terrain and heightmap resolution
		//IMPORTANT: set heightmap resolution equals to map size/resolution so I don't have to resize and do image filterings later
		int size = 512;  //map size: {size}x{size}
		int resolution = size * (doubleResolution ? 2 : 1);
		GetComponent<Terrain> ().terrainData.size = new Vector3 (25, 10, 25);
		GetComponent<Terrain> ().terrainData.heightmapResolution = resolution;

		//request stuff
		string url = "http://maps.googleapis.com/maps/api/staticmap";
		string qs = "";
		string qs2 = "";

		if (centerLocation.address != "") {
			qs += "center=" + WWW.UnEscapeURL(centerLocation.address);
		} else {
			qs += "center=" + WWW.UnEscapeURL (string.Format ("{0},{1}", centerLocation.latitude, centerLocation.longitude));
		}
		qs += "&zoom=" + zoom.ToString ();
		qs += "&size=" + WWW.UnEscapeURL (string.Format ("{0}x{0}", size));
		qs += "&scale=" + (doubleResolution ? "2" : "1");
		qs2 = qs;
		qs += "&maptype=" + mapType.ToString ().ToLower ();
		qs += "&style=" + (style!="" ? style : "element:labels|visibility:off");
		qs2 += "&maptype=terrain";
		qs2 += "&style=" + (style!="" ? style : "visibility:off&style=feature:landscape|visibility:on");
		qs2 += "&style=lightness:" + lightness.ToString();

		WWW req = new WWW (url + "?" + qs);
		WWW req2 = new WWW (url + "?" + qs2);
		yield return req;
		yield return req2;
		ApplyHeightmap(req2);

		//need this to add the texture to the terrain so it looks like a map
		var splatList = new List<SplatPrototype>();
		SplatPrototype newSplat = new SplatPrototype();
		newSplat.texture = req.texture; //satellite: req, heightmap: req2 (use req normally)
		float width = GetComponent<Terrain> ().terrainData.size.x;
		newSplat.tileSize = new Vector2( width, width );
		newSplat.tileOffset = Vector2.zero;
		splatList.Add (newSplat);
		GetComponent<Terrain> ().terrainData.splatPrototypes = splatList.ToArray();
	}

	void ApplyHeightmap(WWW request){

		Texture2D heightmap = request.texture;
		TerrainData terrain = GetComponent<Terrain> ().terrainData;

		int reqWidth = heightmap.width;
		int reqHeight = heightmap.height;
		int widthToBe = terrain.heightmapWidth-1;

		float[,] heightmapData = terrain.GetHeights(0, 0, widthToBe, widthToBe);
		Color[] heightmapColors = heightmap.GetPixels();
		Color[] newMap = new Color[widthToBe * widthToBe];

		float prevY=0;
		float prevX=0;
		float prevXY=0;
		int count=0;
		
		if (widthToBe == reqWidth && reqHeight == reqWidth)  {
			// Use original if no resize is needed
			newMap = heightmapColors;

			// Assign texture data to heightmap
			for (int y = 0; y < widthToBe; y++) {
				for (int x = 0; x < widthToBe; x++) {
					if (y < 25) {
						heightmapData[y, x] = 0; //remove annoying google mountain
					} else {
						//smooth a little using average of previos neighbors					
						prevY = (y==0 ? 0 : heightmapData[y-1,x]);
						prevX = (x==0 ? 0 : heightmapData[y, x-1]);
						prevXY = ((x==0 || y==0) ? 0 : heightmapData[y-1, x-1]);
						count = Mathf.CeilToInt(prevY) + Mathf.CeilToInt(prevX) + Mathf.CeilToInt(prevXY) + 1;
						if (lightness < -80 ){
							heightmapData[y,x] = (newMap[y*widthToBe+x].grayscale + prevX + prevY + prevXY)/count;
						} else {
							heightmapData[y,x] = (1 - (newMap[y*widthToBe+x].grayscale + prevX + prevY + prevXY)/count);
						}
						count = 0;
					}
				}
			}

			//smooooooth !
			int arbitrary = 1; //times I do the smoothing
			int neighbors = 5; //neighbors I want to use for smoothing height values
			float sum =0;
			for (int i=0; i < arbitrary; i++){
				for (int y=0; y < widthToBe; y++){
					for (int x=0; x < widthToBe; x++){
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

			terrain.SetHeights(0, 0, heightmapData);
			} else {
				Debug.Log("IMPOSSIBLE (would need to resize)");
				Debug.Log("google map texture: " + reqWidth + "x" + reqHeight + "\nterrain heightmap: " + widthToBe);
			}
		}
	}

