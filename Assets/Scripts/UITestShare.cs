using UnityEngine;
using UnityEngine.UI;
using OPGames.Share;
using TMPro;

public class UITestShare : MonoBehaviour
{
	public TMP_InputField Input;
	public TMP_InputField InputImgur;
	public TMP_InputField InputWebhook;
	public RectTransform ScreenshotRect;
	public Toggle ScreenshotToggle;

	public Rect GetScreenshotArea()
	{
		Vector2 size = Vector2.Scale(ScreenshotRect.rect.size, transform.lossyScale);
		return new Rect((Vector2)ScreenshotRect.position - (size * 0.5f), size);
	}

	public void OnBtnDiscord()
	{
		ShareUtil su = ShareUtil.Instance;
		su.DiscordWebhook = InputWebhook.text;

		su.PostToDiscord(
				Input.text,
				ScreenshotToggle.isOn,
				GetScreenshotArea());
	}

	public void OnBtnTwitter()
	{
		ShareUtil su = ShareUtil.Instance;
		su.ImgurClientId = InputImgur.text;

		su.PostToTwitter(
				Input.text,
				ScreenshotToggle.isOn,
				GetScreenshotArea());
	}
}
