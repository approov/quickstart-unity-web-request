using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Approov;
using Newtonsoft.Json;

public class BasicWebCall : MonoBehaviour
{
    public Text messageText;
    public InputField scoreToSend;

    readonly string getURL = "https://httpbin.org/headers";
    readonly string expiredURL = "https://expired.badssl.com/";
    readonly string unprotectedURL = "https://mobil.com";
    readonly string selfSignedURL = "https://self-signed.badssl.com/";
    readonly string postURL = "http://homecookedgames.com/tutorialScrips/UWR_Tut_Post.php";

    private void Start()
    {
        messageText.text = "Press buttons to interact with web server";
    }

    public void OnButtonGetScore()
    {
        messageText.text = "Downloading data...";
        StartCoroutine(SimpleGetRequest());
        //service.FetchApproovToken();
    }

    IEnumerator SimpleGetRequest()
    {
        //UnityWebRequest www = UnityWebRequest.Get(getURL);
        ApproovService.Initialize("#dev-ivol#att-dev-ivol.critical.blue#https://dev.approoval.com/token#wzZKRbc75orFGQD6wiIkCadZsyuJjJdwVQWFMVxk1Ow=");
        ApproovWebRequest www = ApproovWebRequest.Get(getURL);
        www.SetRequestHeader("Random-Header","test-key");
        ApproovWebRequest.AddSubstitutionHeader("Random-Header", null);

        yield return www.SendWebRequest();
        if(www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("ERROR FROM SIMPLEGETREQUEST: " + www.error);
        }
        else
        {
            Debug.Log("SUCCESS FROM SIMPLEGETREQUEST: " + www.downloadHandler.text);
            messageText.text = www.downloadHandler.text;
        }
        /*
        Debug.Log("SimpleGetRequest: end function" );
        Debug.Log("TestApproovBridge:   ---- BEGIN ------ \n\n\n\n\n\n" );
        TestApproovBridge();
        Debug.Log("TestApproovBridge:   ---- END ------ \n\n\n\n\n\n" );
        Debug.Log("TestApproovService:   ---- BEGIN ------ \n\n\n\n\n\n" );
        TestApproovService();
        Debug.Log("TestApproovService:   ---- END ------ \n\n\n\n\n\n" );
        */
    }

    public void OnButtonSendScore()
    {
        if (scoreToSend.text == string.Empty)
        {
            messageText.text = "Error: No high score to send.\nEnter a value in the input field.";
        }
        else
        {
            messageText.text = "Sending data...";
            StartCoroutine(SimplePostRequest(scoreToSend.text));
        }
    }

    IEnumerator SimplePostRequest(string curScore)
    {
        List<IMultipartFormSection> wwwForm = new List<IMultipartFormSection>();
        wwwForm.Add(new MultipartFormDataSection("curScoreKey", curScore));
        // Approov
        ApproovService.Initialize("");
        UnityWebRequest www = ApproovWebRequest.Post(postURL, wwwForm);
        ApproovWebRequest request = new ApproovWebRequest("httpbin.org", "GET");
        //UnityWebRequest www = UnityWebRequest.Post(postURL, wwwForm);




        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            messageText.text = www.downloadHandler.text;
        }


    }

    bool TestApproovBridge() {
        // Initialization has to happen at ApproovService level
        if(!ApproovService.IsSDKInitialized()) {
            Debug.LogError("Approov Bridge not initialized");
            return false;
        } else Debug.Log("Approov Bridge initialized");

        // FetchConfig
        Debug.Log("\nApproov Bridge FetchConfig");
        string configString = ApproovBridge.FetchConfig();
        if (configString == null) {
            Debug.LogError("Approov Bridge config string is null");
            return false;
        } else Debug.Log("Approov Bridge config string: " + configString);
        // FetchToken
        Debug.Log("\nApproov Bridge FetchToken");
        ApproovTokenFetchResult result = ApproovBridge.FetchApproovTokenAndWait(getURL);
        Debug.Log("Approov Bridge FetchApproovTokenAndWait: " + result.token);
        // GetPinsJSON
        Debug.Log("\nApproov Bridge GetPinsJSON");
        string pinsJSON = ApproovBridge.GetPinsJSON("public-key-sha256");
        if (pinsJSON == null) {
            Debug.LogError("Approov Bridge pins JSON is null");
            return false;
        } else Debug.Log("Approov Bridge pins JSON: " + pinsJSON);
        // FetchCustomJWT
        Debug.Log("\nApproov Bridge FetchCustomJWT");
        var data = new { sample_key = "sample_value" };
        string jsonString = JsonConvert.SerializeObject(data);
        result = ApproovBridge.FetchCustomJWTAndWait(jsonString);
        Debug.Log("Approov Bridge custom JWT: " + result.token);

        // FetchSecureString
        Debug.Log("\nApproov Bridge FetchSecureString");
        result = ApproovBridge.FetchSecureStringAndWait("word_of_the_day_api_key", null); // Android fails here null string ConvertTokenFetchStatus: 13
        if (result.secureString == null) {
            Debug.LogError("Approov Bridge secure string is null: ");
        } else Debug.Log("Approov Bridge secure string: " + result.secureString);
        // SetDataHashInToken
        Debug.Log("\nApproov Bridge SetDataHashInToken");
        ApproovBridge.SetDataHashInToken("hello-world!");
        // FetchApproovTokenAndWait
        result = ApproovBridge.FetchApproovTokenAndWait(getURL);
        Debug.Log("Approov Bridge token: " + result.token);
        // GetDeviceID
        Debug.Log("\nApproov Bridge GetDeviceID");
        string deviceID = ApproovBridge.GetDeviceID();
        if (deviceID == null) {
            Debug.LogError("Approov Bridge device ID is null");
            return false;
        } else Debug.Log("Approov Bridge device ID: " + deviceID);
        // GetMessageSignature
        Debug.Log("\nApproov Bridge GetMessageSignature");
        string messageSignature = ApproovBridge.GetMessageSignature("hello-world!");
        if (messageSignature == null) {
            Debug.LogError("Approov Bridge message signature is null");
            return false;
        } else Debug.Log("Approov Bridge message signature: " + messageSignature);
        // GetIntegrityMeasurementProof
        Debug.Log("\nApproov Bridge FetchApproovTokenAndWait");
        result = ApproovBridge.FetchApproovTokenAndWait("yahoo.com?measurement");   //ConvertTokenFetchStatus: 0 but token empty?
        Debug.Log("Approov Bridge token: " + result.token);
        if (result.measurementConfig == null) {
            Debug.Log("Approov Bridge measurement config is null!!! ");
        } else {
            Debug.Log("Approov Bridge measurement config length: " + result.measurementConfig.Length);
        }
        Debug.Log("\nApproov Bridge GetIntegrityMeasurementProof ");
        byte[] nonce = new byte[] {1,2,3,4,5,6,7,8,9,9,8,7,6,5,4,3};
        byte[] proof = ApproovBridge.GetIntegrityMeasurementProof(nonce, result.measurementConfig); // NullReferenceException: Object reference not set to an instance of an object.
        if (proof == null) {
            Debug.LogError("Approov Bridge integrity measurement proof is null");
        } else Debug.Log("Approov Bridge integrity measurement proof: " + BitConverter.ToString(proof).Replace("-", " "));
        // GetDeviceMeasyrementProof
        Debug.Log("\nApproov Bridge GetDeviceMeasurementProof ");
        byte[] proofDev = ApproovBridge.GetDeviceMeasurementProof(nonce, result.measurementConfig);
        if (proof == null) {
            Debug.LogError("Approov Bridge device measurement proof is null");
        } else Debug.Log("Approov Bridge device measurement proof: " + BitConverter.ToString(proof).Replace("-", " "));
        return true;
    }

    bool TestApproovService() {
        // Initialization has to happen at ApproovService level
        if(!ApproovService.IsSDKInitialized()) {
            Debug.LogError("Approov Service not initialized");
            return false;
        } else Debug.Log("Approov Service initialized");

        // FetchConfig
        Debug.Log("\nApproov Service FetchConfig");
        string configString = ApproovService.FetchConfig();
        if (configString == null) {
            Debug.LogError("Approov Service config string is null");
            return false;
        } else Debug.Log("Approov Service config string: " + configString);

        // FetchToken
        Debug.Log("\nApproov Service FetchToken");
        string result = ApproovService.FetchToken(getURL);
        Debug.Log("Approov Service FetchApproovTokenAndWait: " + result);
        // GetPinsJSON
        Debug.Log("\nApproov Service GetPinsJSON");
        string pinsJSON = ApproovService.GetPinsJSON("public-key-sha256");
        if (pinsJSON == null) {
            Debug.LogError("Approov Service pins JSON is null");
            return false;
        } else Debug.Log("Approov Service pins JSON: " + pinsJSON);
        // FetchCustomJWT
        Debug.Log("\nApproov Service FetchCustomJWT");
        try {
            var data = new { sample_key = "sample_value" };
            string jsonString = JsonConvert.SerializeObject(data);
            result = ApproovService.FetchCustomJWT(jsonString);
        } catch (Exception e) {
            Debug.LogError("Approov Service custom JWT exception: " + e.Message);
            result = "";
        }

        Debug.Log("Approov Service custom JWT: " + result);

        // FetchSecureString
        Debug.Log("\nApproov Service FetchSecureString");
        try {
            result = ApproovService.FetchSecureString("word_of_the_day_api_key", null);
            if (result == null) {
                Debug.LogError("Approov Service secure string is null: ");
            } else Debug.Log("Approov Service secure string: " + result);
        } catch (Exception e) {
            Debug.LogError("Approov Service secure string exception: " + e.Message);
            result = "";
        }

        // SetDataHashInToken
        Debug.Log("\nApproov Service SetDataHashInToken");
        ApproovBridge.SetDataHashInToken("hello-world!");
        // FetchApproovTokenAndWait
        result = ApproovService.FetchToken(getURL);
        Debug.Log("Approov Service token: " + result);
        // GetDeviceID
        Debug.Log("\nApproov Service GetDeviceID");
        string deviceID = ApproovService.GetDeviceID();
        if (deviceID == null) {
            Debug.LogError("Approov Service device ID is null");
            return false;
        } else Debug.Log("Approov Service device ID: " + deviceID);
        // GetMessageSignature
        Debug.Log("\nApproov Service GetMessageSignature");
        string messageSignature = ApproovService.GetMessageSignature("hello-world!");
        if (messageSignature == null) {
            Debug.LogError("Approov Service message signature is null");
            return false;
        } else Debug.Log("Approov Service message signature: " + messageSignature);
        // GetIntegrityMeasurementProof
        Debug.Log("\nApproov Service FetchApproovTokenAndWait");
        result = ApproovService.FetchToken("yahoo.com?measurement");
        Debug.Log("Approov Service token with measurement: " + result);
        return true;
    } // TestApproovService
}
