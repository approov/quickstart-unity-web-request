using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


public class ShapesApp : MonoBehaviour
{
    public Button helloButton;
    public Button shapesButton;
    // The Text control to display the response
    public Text statusText;
    // The image UI control to display the image
    public Image shapesImage;
    // The API endpoint version: v1 unprotected, v3 protected
    static readonly string apiVersion = "v1";
    // The hello endpoint
    public string helloUrl = "https://shapes.io/" + apiVersion + "/hello";
    public string shapesUrl = "https://shapes.io/" + apiVersion + "/shape";
    // Start is called before the first frame update
    void Start()
    {
        // Assign the OnClick event to the buttons
        helloButton.onClick.AddListener(OnHelloButtonClicked);
        shapesButton.onClick.AddListener(OnShapesButtonClicked);
    }

    void OnHelloButtonClicked()
    {
        StartCoroutine(MakeGetRequest(helloUrl));
    }

    void OnShapesButtonClicked()
    {
        StartCoroutine(MakeGetRequest(shapesUrl));
    }

    IEnumerator MakeGetRequest(string uri)
    {
        UnityWebRequest webRequest = UnityWebRequest.Get(uri);
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(webRequest.error);
            // Set image to error

            // Set text to error
        }
        else
        {
            Debug.Log("Received: " + webRequest.downloadHandler.text);
        }
    }
}
