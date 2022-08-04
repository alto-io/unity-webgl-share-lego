using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Web;
using System.Collections;
using System.Collections.Generic;

namespace OPGames.Share
{

/// This class can be used to easily share to Twitter and Discord. 
/// This works for Unity WebGL builds.
public class ShareUtil : MonoBehaviour
{
	/// This class is simply for deserializing the JSON response from Imgur api call
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

	/// Singleton instance
	static private ShareUtil _instance = null;
	static public ShareUtil Instance { get { return _instance; } }

#region Public
	
	/// Is the screenshot encoded to a JPG or a PNG?
	public bool   IsScreenshotJPG = true;

	/// Supply the Imgur Client ID here
	/// Get a client ID by following the instructions from
	/// https://apidocs.imgur.com/
	public string ImgurClientId = "";

	/// Supply the Discord Webhook URL here
	/// Follow the "Making a Webhook" section of this guide
	/// https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks
	public string DiscordWebhook = "";

	/// Username that will be used when posting to Discord
	public string DiscordUserName = "Discord Bot";
	
	/// Public Image URL that will be used as avatar when posting to Discord
	public string DiscordAvatar = "";

	/// Take a screenshot
	/// <param name="r">The screenshot rectangle area in screen coordinates</param>
	/// <param name="onDone">
	/// This callback will be called when screenshot is done. 
	/// The Texture2D reference will be passed as parameter
	/// </param>
	public IEnumerator TakeScreenshotCR(Rect r, Action<Texture2D> onDone)
	{
		yield return new WaitForEndOfFrame();
		Texture2D ss = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGB24, false );
		ss.ReadPixels(r, 0, 0 );
		ss.Apply();

		if (onDone != null)
			onDone(ss);
	}

	/// Convenience function for the coroutine TakeScreenshotCR
	public void TakeScreenshot(Rect r, Action<Texture2D> onDone)
	{
		StartCoroutine(TakeScreenshotCR(r, onDone));
	}

	/// Post the screenshot to Imgur
	/// <param name="ss">The texture for the screenshot previously captured</param>
	/// <param name="onDone">
	/// This callback will be called when posting to Imgur is done. 
	/// The url of the posted image will be passed as parameter
	/// </param>
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

	/// Convenience function for the coroutine PostToImgurCR
	public void PostToImgur(Texture2D ss, Action<string> onDone)
	{
		StartCoroutine(PostToImgurCR(ss, onDone));
	}

	/// Uses the Twitter Web Intent to open up a browser tab pre-filled with a tweet
	/// https://developer.twitter.com/en/docs/twitter-for-websites/web-intents/overview
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

	/// Convenience function to do all of the following:
	/// 1. Take a screenshot if needed
	/// 2. Post screenshot to Imgur
	/// 3. Open a new tweet pre-filled with the text and screenshot url
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

	/// Convenience function to call PostToTwitterCR
	public void PostToTwitter(string text, bool screenshot, Rect r)
	{
		StartCoroutine(PostToTwitterCR(text, screenshot, r));
	}

	/// Post to discord by doing the following steps
	/// 1. Take a screenshot if needed
	/// 2. Post text and screenshot to Discord
	/// <param name="text">The text to post</param>
	/// <param name="screenshot">Do we need to take a screenshot?</param>
	/// <param name="r">Rectangle area to screenshot, in screen coordinates</param>
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

	/// Convenience function to call PostToDiscordCR
	public void PostToDiscord(string text, bool screenshot, Rect r)
	{
		StartCoroutine(PostToDiscordCR(text, screenshot, r));
	}

	/// Post to discord
	/// <param name="text">The text to post</param>
	/// <param name="username">Username that will be used when posting</param>
	/// <param name="bytes">The byte array of the image data</param>
	/// <param name="filename">
	///     Filename that will be assigned to the image. This must 
	///     have the correct extension that corresponds to the image 
	///     data (i.e. .jpg or .png)
	/// </param>
	/// <param name="onDone">
	///     Callback that will be called when posting is done. Bool 
	///     parameter to indicate if post was successful or not
	/// </param>
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

	/// Convenience function to call PostToDiscordCR
	public void PostToDiscord(string text, string username, byte[] bytes, string filename, Action<bool> onDone)
	{
		StartCoroutine(PostToDiscordCR(text, username, bytes, filename, onDone));
	}

#endregion // Public

#region Private

	/// Makes sure that there's only one instance of ShareUtil
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

#endregion // Private
}

}
