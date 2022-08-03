//https://forum.unity.com/threads/webgl-copy-paste-for-input-field-not-working.494765/
mergeInto(LibraryManager.library, {
 
  JSPasteImgur: function (sometext) {
    navigator.clipboard.readText().then(function(s) {
	  SendMessage("UITestShare", "PasteImgur", s);
	});
  },

  JSPasteWebhook: function (sometext) {
    navigator.clipboard.readText().then(function(s) {
	  SendMessage("UITestShare", "PasteWebhook", s);
	});
  },
 
});
