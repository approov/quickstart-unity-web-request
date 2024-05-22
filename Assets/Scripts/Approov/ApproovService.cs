using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


namespace Approov
{
    // We need the classes in order to use JsonUtility and deserialize the JSON response
    // from getPinsJson method
    [System.Serializable]
    public class KeyValuePair
    {
        public string key;
        public List<string> value;
    }
    /*  ApproovService class implements C# interface to the Approov SDK
    *   by indirectly calling the bridging layer defined in ApproovBridge.cs
    */
    public class ApproovService : MonoBehaviour
    {
        // The config string used to initialize the SDK
        private static string sConfigStringUsed = null;
        /* Lock object: used during ApproovSDK init call */
        protected static readonly object InitializerLock = new();
        // The log tag
        public static readonly string TAG = "ApproovService ";
        /* Status of Approov SDK initialisation */
        protected static bool ApproovSDKInitialized = false;

        /*  
        *   Initializes the Approov SDK with provided config string
        *   Can throw an initialization failure exception
        *   @param config string with the configuration
        *
        */
        public static void Initialize(string config){
            lock (InitializerLock)
            {
                // Check if attempting to use a different config string
                if (ApproovSDKInitialized)
                {
                    // Check if attempting to use a different config string
                        if ((sConfigStringUsed != null) && (sConfigStringUsed != config))
                        {
                            throw new ConfigurationFailureException(TAG + "Error: SDK already initialized");
                        }
                } else {
                    // Initialize the SDK
                    #if UNITY_ANDROID
                    ApproovBridge.Initialize(config);
                    #elif UNITY_IOS
                    // iOS
                    bool statusInit = ApproovBridge.Initialize(config, "auto", null, out var e);
                    if (!statusInit)
                    {
                        throw new InitializationFailureException(TAG + "Error SDK initialization failed", false);
                    } 
                    #endif
                    // Set user property to id the service
                    SetUserProperty("approov-service-unity");
                    // Sotre the config string used
                    sConfigStringUsed = config;
                    // Update the status
                    ApproovSDKInitialized = true;
                }
            }  
        }// Initialize method
        
        /*  Get initialization status of SDK
        *   @param true if the SDK is initialized
        */
        public static bool IsSDKInitialized()
        {
            lock (InitializerLock)
            {
                return ApproovSDKInitialized;
            }
        }
        /**
        * Sets a user defined property on the SDK. This may provide information about the
        * app state or aspects of the environment it is running in. This has no direct
        * impact on Approov except it is visible as a property on attesting devices and
        * can be analyzed using device filters. Note that properties longer than 128
        * characters are ignored and all non ASCII characters are removed. The special
        * value "$error" may be used to mark an error condition for offline measurement
        * mismatches.
        *
        * @param property to be set, which may be null
        */
        public static void SetUserProperty(string property)  {
            ApproovBridge.SetUserProperty(property);
        }

        #if UNITY_ANDROID
        /**
        * Sets the information about a current activity. This may be set for an expected app
        * launch activity so that analysis can be performed to determine if the activity may have
        * been launched in an automatic way. A flag indicating this can then be included as an
        * annotation in the Approov token.
        *
        * @param activity is the current activity that is being run which must be obtained using: 
        *   AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        *   AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        *   ApproovService.SetActivity(currentActivity);
        */

        public static void SetActivity(AndroidJavaObject activity) {
            ApproovBridge.SetActivity(activity);
        }
        #endif
        /*
        *  Allows token prefetch operation to be performed as early as possible. This
        *  permits a token to be available while an application might be loading resources
        *  or is awaiting user input. Since the initial token fetch is the most
        *  expensive the prefetch seems reasonable.
        */
        public static void Prefetch() {
            lock (InitializerLock) {
                if (ApproovSDKInitialized) {
                    _ = HandleTokenFetchAsync();
                } 
            }
        }

        private static async Task HandleTokenFetchAsync()
        {
            _ = await Task.Run(() => FetchToken("approov.io"));
        }

        // MARK: Approov SDK methods
        // Auxiliary method to print FetchStatus from Approov SDK
        public static string ApproovTokenFetchStatusToString(ApproovTokenFetchStatus status)
        {
            switch (status)
            {
                case ApproovTokenFetchStatus.Success:
                    return "SUCCESS";
                case ApproovTokenFetchStatus.NoNetwork:
                    return "NO_NETWORK";
                case ApproovTokenFetchStatus.MITMDetected:
                    return "MITM_DETECTED";
                case ApproovTokenFetchStatus.PoorNetwork:
                    return "POOR_NETWORK";
                case ApproovTokenFetchStatus.NoApproovService:
                    return "NO_APPROOV_SERVICE";
                case ApproovTokenFetchStatus.BadURL:
                    return "BAD_URL";
                case ApproovTokenFetchStatus.UnknownURL:
                    return "UNKNOWN_URL";
                case ApproovTokenFetchStatus.UnprotectedURL:
                    return "UNPROTECTED_URL";
                case ApproovTokenFetchStatus.NoNetworkPermission:
                    return "NO_NETWORK_PERMISSION";
                case ApproovTokenFetchStatus.MissingLibDependency:
                    return "MISSING_LIB_DEPENDENCY";
                case ApproovTokenFetchStatus.InternalError:
                    return "INTERNAL_ERROR";
                case ApproovTokenFetchStatus.Rejected:
                    return "REJECTED";
                case ApproovTokenFetchStatus.Disabled:
                    return "DISABLED";
                case ApproovTokenFetchStatus.UnknownKey:
                    return "UNKNOWN_KEY";
                default:
                    return "UNKNOWN";
            }
        }

        /*
        * Fetches a secure string with the given key. If newDef is not nil then a secure string for
        * the particular app instance may be defined. In this case the new value is returned as the
        * secure string. Use of an empty string for newDef removes the string entry. Note that this
        * call may require network transaction and thus may block for some time, so should not be called
        * from the UI thread. If the attestation fails for any reason then an exception is raised. Note
        * that the returned string should NEVER be cached by your app, you should call this function when
        * it is needed.
        *
        * @param key is the secure string key to be looked up
        * @param newDef is any new definition for the secure string, or nil for lookup only
        * @return secure string (should not be cached by your app) or nil if it was not defined or an error ocurred
        * @throws exception with description of cause
        */
        public static string FetchSecureString(string key, string newDef)
        {
            string type = "lookup";
            if (newDef != null)
            {
                type = "definition";
            }

            ApproovTokenFetchResult fetchResult = ApproovBridge.FetchSecureStringAndWait(key, newDef);
            ApproovTokenFetchStatus fetchStatus = fetchResult.status;

            // Check the status
            Console.WriteLine(TAG + "FetchSecureString: " + type + " " + ApproovTokenFetchStatusToString((ApproovTokenFetchStatus)fetchStatus));
            if (fetchStatus == ApproovTokenFetchStatus.Disabled)
            {
                throw new ConfigurationFailureException(TAG + "FetchSecureString:  secure message string feature is disabled");
            }
            else if (fetchStatus== ApproovTokenFetchStatus.UnknownKey)
            {
                throw new ConfigurationFailureException(TAG + "FetchSecureString: secure string unknown key");
            }
            else if (fetchStatus== ApproovTokenFetchStatus.Rejected)
            {
                // if the request is rejected then we provide a special exception with additional information 
                string localARC = fetchResult.ARC;
                string localReasons = fetchResult.rejectionReasons;
                throw new RejectionException(TAG + "FetchSecureString: secure message rejected", arc: localARC, rejectionReasons: localReasons);
            }
            else if (fetchStatus== ApproovTokenFetchStatus.NoNetwork ||
                    fetchStatus== ApproovTokenFetchStatus.PoorNetwork ||
                    fetchStatus== ApproovTokenFetchStatus.MITMDetected)
            {
                /* We are unable to get the secure string due to network conditions so the request can
                *  be retried by the user later
                *  We are unable to get the secure string due to network conditions, so we must not proceed. The request can be retried by the user later.
                */

                // We throw
                throw new NetworkingErrorException(TAG + "FetchSecureString: network issue, retry needed");

            }
            else if ((fetchStatus!= (int)ApproovTokenFetchStatus.Success) &&
                    fetchStatus!= ApproovTokenFetchStatus.UnknownKey)
            {
                // we have failed to get a secure string with a more serious permanent error
                throw new PermanentException(TAG + "FetchSecureString: " + ApproovTokenFetchStatusToString((ApproovTokenFetchStatus)fetchStatus));
            }
            // Call getSecureString
            string secureStringStr = fetchResult.secureString;
            return secureStringStr;
        }//FetchSecureString

        /*
        * Fetches a custom JWT with the given payload. Note that this call will require network
        * transaction and thus will block for some time, so should not be called from the UI thread.
        * If the fetch fails for any reason an exception will be thrown. 
        *
        * @param payload is the marshaled JSON object for the claims to be included
        * @return custom JWT string or nil if an error occurred
        * @throws exception with description of cause
        */
        public static string FetchCustomJWT(string payload)
        {
            ApproovTokenFetchResult fetchResult;
            ApproovTokenFetchStatus aCurrentFetchStatus = ApproovTokenFetchStatus.NoApproovService  ;
            try {
            fetchResult = ApproovBridge.FetchCustomJWTAndWait(payload);
            aCurrentFetchStatus = fetchResult.status;
            } catch (Exception e) {
                Console.WriteLine(TAG + "FetchCustomJWT: " + e.Message);
                throw new PermanentException(TAG + "FetchCustomJWT: " + e.Message);
            }
            Console.WriteLine(TAG + "FetchCustomJWT: " + ApproovTokenFetchStatusToString((ApproovTokenFetchStatus)aCurrentFetchStatus));
            if (aCurrentFetchStatus == ApproovTokenFetchStatus.Disabled)
            {
                throw new ConfigurationFailureException(TAG + "FetchCustomJWT: feature not enabled");
            }
            else if (aCurrentFetchStatus == ApproovTokenFetchStatus.Rejected)
            {
                string localARC = fetchResult.ARC;
                string localReasons = fetchResult.rejectionReasons;
                
                // if the request is rejected then we provide a special exception with additional information
                throw new RejectionException(TAG + "FetchCustomJWT: rejected", arc: localARC, rejectionReasons: localReasons);
            }
            else if (aCurrentFetchStatus == ApproovTokenFetchStatus.NoNetwork ||
                    aCurrentFetchStatus == ApproovTokenFetchStatus.PoorNetwork ||
                    aCurrentFetchStatus == ApproovTokenFetchStatus.MITMDetected)
            {
                /* We are unable to get the secure string due to network conditions so the request can
                *  be retried by the user later
                *  We are unable to get the secure string due to network conditions, so we must not proceed. The request can be retried by the user later.
                */
                // We throw
                throw new NetworkingErrorException(TAG + "FetchCustomJWT: network issue, retry needed");

            }
            else if (aCurrentFetchStatus != ApproovTokenFetchStatus.Success)
            {
                throw new PermanentException(TAG + "FetchCustomJWT: " + ApproovTokenFetchStatusToString((ApproovTokenFetchStatus)aCurrentFetchStatus));
            }
            
            return fetchResult.token;
        }// FetchCustomJWT

        /*
        * Performs a precheck to determine if the app will pass attestation. This requires secure
        * strings to be enabled for the account, although no strings need to be set up. This will
        * likely require network access so may take some time to complete. It may throw an exception
        * if the precheck fails or if there is some other problem. 
        */
        public static void Precheck()
        {
            
            ApproovTokenFetchResult fetchResult = ApproovBridge.FetchSecureStringAndWait("precheck-dummy-key", null);
            ApproovTokenFetchStatus aCurrentFetchStatus = fetchResult.status;
            
            // Process the result
            if (aCurrentFetchStatus == ApproovTokenFetchStatus.Rejected)
            {
                // if the request is rejected then we provide a special exception with additional information
                string localARC = fetchResult.ARC;
                string localReasons = fetchResult.rejectionReasons;
                
                throw new RejectionException(TAG + "Precheck: rejected ", arc: localARC, rejectionReasons: localReasons);
            }
            else if (aCurrentFetchStatus == ApproovTokenFetchStatus.NoNetwork ||
                aCurrentFetchStatus == ApproovTokenFetchStatus.PoorNetwork ||
                aCurrentFetchStatus == ApproovTokenFetchStatus.MITMDetected)
            {
                throw new NetworkingErrorException(TAG + "Precheck: network issue, retry needed");
            }
            else if ((aCurrentFetchStatus != ApproovTokenFetchStatus.Success) &&
                    aCurrentFetchStatus != ApproovTokenFetchStatus.UnknownKey)
            {
                throw new PermanentException(TAG + "Precheck: " + ApproovTokenFetchStatusToString(aCurrentFetchStatus));
            }
            // Get loggable token and print
            string loggableToken = fetchResult.loggableToken;
            
            Console.WriteLine(TAG + "Precheck " + loggableToken);
        }// Precheck

        /**
        * Gets the device ID used by Approov to identify the particular device that the SDK is running on. Note
        * that different Approov apps on the same device will return a different ID. Moreover, the ID may be
        * changed by an uninstall and reinstall of the app.
        *
        * @return String of the device ID or null in case of an error
        */
        public static string GetDeviceID()
        {
            string deviceID = ApproovBridge.GetDeviceID();
            Console.WriteLine(TAG + "DeviceID: " + deviceID);
            return deviceID;
        }

        /**
        * Directly sets the data hash to be included in subsequently fetched Approov tokens. If the hash is
        * different from any previously set value then this will cause the next token fetch operation to
        * fetch a new token with the correct payload data hash. The hash appears in the
        * 'pay' claim of the Approov token as a base64 encoded string of the SHA256 hash of the
        * data. Note that the data is hashed locally and never sent to the Approov cloud service.
        *
        * @param data is the data to be hashed and set in the token
        */
        public static void SetDataHashInToken(string data)
        {
            Console.WriteLine(TAG + "SetDataHashInToken");
            ApproovBridge.SetDataHashInToken(data);
        }

        /**
        * Gets the signature for the given message. This uses an account specific message signing key that is
        * transmitted to the SDK after a successful fetch if the facility is enabled for the account. Note
        * that if the attestation failed then the signing key provided is actually random so that the
        * signature will be incorrect. An Approov token should always be included in the message
        * being signed and sent alongside this signature to prevent replay attacks. If no signature is
        * available, because there has been no prior fetch or the feature is not enabled, then an
        * ApproovException is thrown.
        *
        * @param message is the message whose content is to be signed
        * @return String of the base64 encoded message signature
        */
        public static string GetMessageSignature(string message)
        {
            Console.WriteLine(TAG + "GetMessageSignature");
            string signature = ApproovBridge.GetMessageSignature(message);
            return signature;
        }

        /**
        * Performs an Approov token fetch for the given URL. This should be used in situations where it
        * is not possible to use the networking interception to add the token. This will
        * likely require network access so may take some time to complete. If the attestation fails
        * for any reason then an Exception is thrown. ... Note that
        * the returned token should NEVER be cached by your app, you should call this function when
        * it is needed.
        *
        * @param url is the URL giving the domain for the token fetch
        * @return string    jwt token from token fetch
        * @throws Exception if there was a problem
        */

        public static string FetchToken(string url)
        {
            // Invoke fetchApproovTokenAndWait
            ApproovTokenFetchResult fetchResult = ApproovBridge.FetchApproovTokenAndWait(url);
            ApproovTokenFetchStatus aCurrentFetchStatus = fetchResult.status;

            // Process the result
            Console.WriteLine(TAG + "FetchToken: " + url + " " + ApproovTokenFetchStatusToString(aCurrentFetchStatus));
            if (aCurrentFetchStatus == ApproovTokenFetchStatus.Success) {
                string token = fetchResult.token;
                return token;
            } else if ( aCurrentFetchStatus == ApproovTokenFetchStatus.NoNetwork || 
                        aCurrentFetchStatus == ApproovTokenFetchStatus.PoorNetwork ||
                        aCurrentFetchStatus == ApproovTokenFetchStatus.MITMDetected) {
                            throw new NetworkingErrorException(TAG + "FetchToken: networking error, retry needed");
            } else {
                throw new PermanentException(TAG + "FetchToken: " + ApproovTokenFetchStatusToString(aCurrentFetchStatus));
            }
        }// FetchToken

        /*  Get set of pins from Approov SDK in JSON format
        *   @param pinType is the type of pin to be fetched
        *   @return JSON string with the pins
        */
        public static string GetPinsJSON(string pinType)
        {   
            string approovPinsJNI = ApproovBridge.GetPinsJSON(pinType);
            return approovPinsJNI;
        }

        /**
        * Fetches the current configuration for the SDK. This may be the initial configuration or may
        * be a new updated configuration returned from the Approov cloud service. Such updates of the
        * configuration allow new sets of certificate pins and other configuration to be passed to
        * an app instance that is running in the field.
        *
        * Normally this method returns the latest configuration that is available and is cached in the
        * SDK. Thus the method will return quickly. However, if this method is called when there has
        * been no prior call to fetch an Approov token then a network request to the Approov cloud
        * service will be made to obtain any latest configuration update. The maximum timeout period
        * is set to be quite short but the caller must be aware that this delay may occur.
        *
        * Note that the returned configuration should generally be kept in local storage for the app
        * so that it can be made available on initialization of the Approov SDK next time the app
        * is started.
        *
        * It is possible to see if a new configuration becomes available from the isConfigChanged()
        * method of the TokenFetchResult. This changed flag is only cleared for future token fetches
        * if a call to this method is made.
        *
        * @return String representation of the configuration
        */
        public static string FetchConfig()
        {
            string config = ApproovBridge.FetchConfig();
            return config;
        }
        /**
        * Sets a development key on the SDK. This may provide a key indicating that
        * the app is a development version and it should pass attestation even
        * if the app is not registered or it is running on an emulator. The development
        * key value can be rotated at any point in the account if a version of the app
        * containing the development key is accidentally released. This is primarily
        * used for situations where the app package must be modified or resigned in
        * some way as part of the testing process.
        *
        * @param key is the development key value to be set, which may be null
        */
        public static void SetDevKey(string key) {
            ApproovBridge.SetDevKey(key);
        }

        /**
        * Obtains an integrity measurement proof that is used to show that the app and its
        * environment have not changed since the time of the original integrity measurement.
        * The proof does an HMAC calculation over the secret integrity measurement value which
        * is salted by a provided nonce. This proves that the SDK is able to reproduce the
        * integrity measurement value.
        *
        * @param nonce is a 16-byte (128-bit) nonce value used to salt the proof HMAC
        * @param measurementConfig is the measurement configuration obtained from a previous token fetch results
        * @return 32-byte (256-bit) measurement proof value
        */
        public static byte[] GetIntegrityMeasurementProof(byte[] nonce, byte[] measurementConfig) {
            byte[] proof = ApproovBridge.GetIntegrityMeasurementProof(nonce, measurementConfig);
            return proof;
        }

        /**
        * Obtains a device measurement proof that is used to show that the device environment
        * has not changed since the time of the original integrity measurement. This allows the
        * app version, including the Approov SDK, to be updated while preserving the device
        * measurement. The proof does an HMAC calculation over the secret device measurement
        * value which is salted by a provided nonce. This proves that the SDK is able to reproduce
        * the device measurement value.
        *
        * @param nonce is a 16-byte (128-bit) nonce value used to salt the proof HMAC
        * @param measurementConfig is the measurement configuration obtained from a previous token fetch results
        * @return 32-byte (256-bit) measurement proof value
        */
        public static byte[] GetDeviceMeasurementProof(byte[] nonce, byte[] measurementConfig) {
            byte[] proof = ApproovBridge.GetDeviceMeasurementProof(nonce, measurementConfig);
            return proof;
        }
        // MARK: END Approov API related methods
    }// ApproovService class

}// namespace Approov