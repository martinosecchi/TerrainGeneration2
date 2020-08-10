using UnityEngine;
using System.Collections;

public class GoogleMap : MonoBehaviour
{
	public enum MapType
	{
		Satellite,
		RoadMap,
		Hybrid,
		Terrain
	}
	public bool loadOnStart = true;
	public GoogleMapLocation centerLocation;
	public int zoom = 13;
	public MapType mapType;
	public int size = 512;
	public bool doubleResolution = false;
	public int lightness = -100;
	public string style = "";

	void Start() {
		if(loadOnStart) Refresh();	
	}
	
	public void Refresh() {
		StartCoroutine(_Refresh());
	}
	
	IEnumerator _Refresh ()
	{
		var url = "http://maps.googleapis.com/maps/api/staticmap";
		var qs = "";
		
		if (centerLocation.address != "")
		qs += "center=" + WWW.UnEscapeURL (centerLocation.address);
		else {
			qs += "center=" + WWW.UnEscapeURL (string.Format ("{0},{1}", centerLocation.latitude, centerLocation.longitude));
		}
		qs += "&zoom=" + zoom.ToString ();
		qs += "&size=" + WWW.UnEscapeURL (string.Format ("{0}x{0}", size));
		qs += "&scale=" + (doubleResolution ? "2" : "1");
		qs += "&maptype=" + mapType.ToString ().ToLower ();
		qs += "&maptype=terrain";
		var usingSensor = false;
		qs += "&sensor=" + (usingSensor ? "true" : "false");
		qs += "&style=" + (style != "" ? style : "visibility:off&style=feature:landscape|visibility:on");
		qs += "&style=lightness:" + lightness.ToString();

		var req = new WWW (url + "?" + qs);
		yield return req;
		GetComponent<Renderer>().material.mainTexture = req.texture;
	}
	
	
}

