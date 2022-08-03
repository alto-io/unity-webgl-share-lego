using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Web;
using System.Collections;
using System.Collections.Generic;

namespace OPGames.Share
{

public class ShareUtil : MonoBehaviour
{
	[System.Serializable]
	public class ImgurUploadResponse
	{
		[System.Serializable]
		public class Data 
		{
			public string id;
			public string link;
		}

		public int status;
		public bool success;
		public Data data;
	}

	static private ShareUtil _instance = null;
	static public ShareUtil Instance { get { return _instance; } }

#region Public
	public bool   IsScreenshotJPG = true;
	public string ImgurClientId = "";

	public string DiscordWebhook = "";
	public string DiscordUserName = "Discord Bot";
	public string DiscordAvatar = "";

	public void TakeScreenshot(Rect r, Action<Texture2D> onDone)
	{
		StartCoroutine(TakeScreenshotCR(r, onDone));
	}

	// be sure to clean up after use
	// UnityEngine.Object.Destroy(texture);
	public IEnumerator TakeScreenshotCR(Rect r, Action<Texture2D> onDone)
	{
		Debug.Log($"Capture screenshot area {r}");
		yield return new WaitForEndOfFrame();
		Texture2D ss = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGB24, false );
		ss.ReadPixels(r, 0, 0 );
		ss.Apply();

		if (onDone != null)
			onDone(ss);
	}

	public void PostToImgur(Texture2D ss, Action<string> onDone)
	{
		StartCoroutine(PostToImgurCR(ss, onDone));
	}

	public IEnumerator PostToImgurCR(Texture2D ss, Action<string> onDone)
	{
		if (ss == null)
		{
			if (onDone != null) onDone("");
			yield break;
		}

		if (string.IsNullOrEmpty(ImgurClientId))
		{
			Debug.LogWarning("Imgur Client ID is empty");
			if (onDone != null) onDone("");
			yield break;
		}

		byte[] bytes = IsScreenshotJPG ? ss.EncodeToJPG() : ss.EncodeToPNG();

		WWWForm form = new WWWForm();
		form.AddField("image", Convert.ToBase64String(bytes));
		form.AddField("type", "base64");

		string url = "https://api.imgur.com/3/image";
		using (var www = UnityWebRequest.Post(url, form))
		{
			www.SetRequestHeader("Authorization", $"Client-ID {ImgurClientId}");
			yield return www.SendWebRequest();

			var json = www.downloadHandler.text;
			if (string.IsNullOrEmpty(www.error) == false)
			{
				Debug.LogError(www.error);
				if (onDone != null) onDone("");
				yield break;
			}

			ImgurUploadResponse response = JsonUtility.FromJson<ImgurUploadResponse>(json);
			if (response == null)
			{
				Debug.LogError($"Json response is invalid {json}");
				if (onDone != null) onDone("");
				yield break;
			}

			if (response.data != null)
			{
				if (onDone != null) onDone(response.data.link);
				yield break;
			}
		}

		if (onDone != null) onDone("");
	}

	public void PostToTwitter(string text, string screenshotUrl)
	{
		text = HttpUtility.UrlEncode(text);
		if (string.IsNullOrEmpty(screenshotUrl) == false)
		{
			// remove the extension
			int index = screenshotUrl.LastIndexOf(".");
			
			if (screenshotUrl.Length - index <= 5)
				screenshotUrl = screenshotUrl.Substring(0, index);

			text = text + " " + screenshotUrl;
		}

		string url = $"https://twitter.com/intent/tweet?text={text}";
		Application.OpenURL(url);
	}

	public void PostToTwitter(string text, bool screenshot, Rect r)
	{
		StartCoroutine(PostToTwitterCR(text, screenshot, r));
	}

	public IEnumerator PostToTwitterCR(string text, bool screenshot, Rect r)
	{
		string screenshotUrl = "";
		if (screenshot)
		{
			Texture2D ss = null;
			yield return StartCoroutine(TakeScreenshotCR(r, (tex) => ss = tex));
			yield return StartCoroutine(PostToImgurCR(ss, (url) => screenshotUrl = url));
			UnityEngine.Object.Destroy( ss );
		}
		PostToTwitter(text, screenshotUrl);
	}

	public void PostToDiscord(string text, bool screenshot, Rect r)
	{
		StartCoroutine(PostToDiscordCR(text, screenshot, r));
	}

	public IEnumerator PostToDiscordCR(string text, bool screenshot, Rect r)
	{
		Texture2D ss = null;
		if (screenshot)
			yield return StartCoroutine(TakeScreenshotCR(r, (tex) => ss = tex));

		byte[] bytes = null;
		if (ss != null)
			bytes = IsScreenshotJPG ? ss.EncodeToJPG() : ss.EncodeToPNG();

		string filename = IsScreenshotJPG ? "attachment.jpg" : "attachment.png";
		
		yield return StartCoroutine(PostToDiscordCR(text, DiscordUserName, bytes, filename, null));
	}

	public void PostToDiscord(string text, string username, byte[] bytes, string filename, Action<bool> onDone)
	{
		StartCoroutine(PostToDiscordCR(text, username, bytes, filename, onDone));
	}

	public IEnumerator PostToDiscordCR(string text, string username, byte[] bytes, string filename, Action<bool> onDone)
	{
		if (string.IsNullOrEmpty(DiscordWebhook))
		{
			Debug.LogWarning("Discord webhook is empty");
			if (onDone != null) onDone(false);
			yield break;
		}

		if (string.IsNullOrEmpty(username))
			username = DiscordUserName;

		List<IMultipartFormSection> form = new List<IMultipartFormSection>();
		form.Add(new MultipartFormDataSection("username", username));
		form.Add(new MultipartFormDataSection("content", text));
		form.Add(new MultipartFormDataSection("avatar_url", DiscordAvatar));

		if (bytes != null)
			form.Add(new MultipartFormFileSection("files[0]", bytes, filename, "application/octet-stream"));

		using (var www = UnityWebRequest.Post(DiscordWebhook, form))
		{
			yield return www.SendWebRequest();
			if (www.result != UnityWebRequest.Result.Success) 
			{
				Debug.LogError(www.error);
				if (onDone != null)
					onDone(false);
			}
		}

		if (onDone != null)
			onDone(true);
	}


#endregion // Public

#region Private

	private void Awake()
	{
		if (_instance == null)
		{
			_instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}


//	// From relative coordinates [0,1], get actual rect based on screen
//	public Rect GetSSRectFromRelative(Vector2 topLeft, Vector2 bottomRight)
//	{
//		Rect result;
//		topLeft.x *= Screen.width;
//		topLeft.y *= Screen.height;
//
//		bottomRight.x *= Screen.width;
//		bottomRight.y *= Screen.height;
//
//		return new Rect(
//				Mathf.Round(topLeft.x),
//				Mathf.Round(topLeft.y),
//				Mathf.Round(bottomRight.x - topLeft.x),
//				Mathf.Round(bottomRight.y - topLeft.y));
//
//	}



#endregion // Private
}

}
