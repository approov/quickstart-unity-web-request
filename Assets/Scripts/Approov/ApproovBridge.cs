using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

/* The ApproovBridge class bridges C# to Java and iOS Objective-C code.
 * It provides the necessary methods to call the native functions from the managed code.
 * The class is defined as a MonoBehaviour to be able to use the Unity API.
 * The class is defined in the Approov namespace to avoid conflicts with other classes.
 * For full descriptions of the methods and parameters/return types, see the ApproovService.cs file.
 */


namespace Approov {
    // Define the structure for ApproovTokenFetchResult for both Android and iOS
    [Serializable]
    public struct ApproovTokenFetchResult
    {
        public ApproovTokenFetchStatus status;
        public string ARC;
        public bool isForceApplyPins;
        public string token;
        public string rejectionReasons;
        public bool isConfigChanged;
        public string secureString;
        public byte[] measurementConfig;
        public string loggableToken;
    }

    // Define the enumeration for ApproovTokenFetchStatus for iOS and Android
    // NOTE: this maps to the objective-c enum ApproovTokenFetchStatus from Success to InternalError
    // and has additional elements that only apply to Java: NO_NETWORK_PERMISSION and MISSING_LIB_DEPENDENCY
    // There is a conversion method in the ApproovBridge class to convert the integer value to the
    // corresponding enum value.
    public enum ApproovTokenFetchStatus
    {
        Success,
        NoNetwork,
        MITMDetected,
        PoorNetwork,
        NoApproovService,
        BadURL,
        UnknownURL,
        UnprotectedURL,
        NotInitialized,
        Rejected,
        Disabled,
        UnknownKey,
        BadKey,
        BadPayload,
        InternalError,
        NoNetworkPermission,
        MissingLibDependency
    }


    public class ApproovBridge : MonoBehaviour
    {
        public static readonly string TAG = "ApproovBridge: ";
        // Success string defined in java and objective-c
        private static readonly string SUCCESS = "SUCCESS";
        /* Type of server certificates supported by Approov SDK */
        public static readonly string kPinTypePublicKeySha256 = "public-key-sha256";

#if UNITY_IOS
        // Import the Objective-C methods
        // + (nonnull NSString *)stringFromApproovTokenFetchStatus:(ApproovTokenFetchStatus)approovTokenFetchStatus;
        [DllImport("__Internal")]
        private static extern IntPtr Approov_stringFromApproovTokenFetchStatus(int approovTokenFetchStatus);

        public static string StringFromApproovTokenFetchStatus(ApproovTokenFetchStatus approovTokenFetchStatus)
        {
            // Call the native function and get the pointer to the NSString
            IntPtr stringPtr = Approov_stringFromApproovTokenFetchStatus((int)approovTokenFetchStatus);

            // Convert the NSString pointer to a managed string
            string status = Marshal.PtrToStringAuto(stringPtr);

            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(stringPtr);

            return status;
        }

        // + (nullable NSString *)fetchConfig;
        [DllImport("__Internal")]
        private static extern IntPtr Approov_fetchConfig();

        public static string FetchConfig()
        {
            // Call the native function and get the pointer to the NSString
            IntPtr stringPtr = Approov_fetchConfig();

            // Convert the NSString pointer to a managed string
            string config = Marshal.PtrToStringAuto(stringPtr);

            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(stringPtr);

            return config;
        }

        // + (nullable NSString *)getPinsJSON:(nonnull NSString *)pinType;
        [DllImport("__Internal")]
        private static extern IntPtr Approov_getPinsJSON(string pinType);

        public static string GetPinsJSON(string pinType)
        {
            // Call the native function and get the pointer to the NSString
            IntPtr stringPtr = Approov_getPinsJSON(pinType);

            // Convert the NSString pointer to a managed string
            string config = Marshal.PtrToStringAuto(stringPtr);

            // Free the unmanaged memory allocated by the Objective-C function
            // Note: This step is necessary to prevent memory leaks
            Marshal.FreeHGlobal(stringPtr);

            return config;
        }

        // + (BOOL)initialize:(nonnull NSString *)initialConfig updateConfig:(nullable NSString *)updateConfig
        //comment:(nullable NSString * )comment error:(NSError *_Nullable *_Nullable)error;
        [DllImport("__Internal")]
        public static extern bool Approov_initialize(string initialConfig, string updateConfig, string comment, out IntPtr error);

        public static bool Initialize(string initialConfig, string updateConfig, string comment, out IntPtr error)
        {
            return Approov_initialize(initialConfig, updateConfig, comment, out error);
        }

        // + (void)fetchApproovToken:(nonnull ApproovTokenFetchCallback)callbackHandler :(nonnull NSString *)url;
        public static void FetchApproovToken(Action<IntPtr> callback, string url)
        {
            // Call the native function
            IntPtr resultPtr = Approov_fetchApproovTokenAndWait(url);
            // Convert the NSString pointer to a managed string
            string jsonString = Marshal.PtrToStringAuto(resultPtr);
            // Deserialize the JSON string into a dictionary
            ApproovTokenFetchResult result = JsonUtility.FromJson<ApproovTokenFetchResult>(jsonString);
            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(resultPtr);
            // Allocate unmanaged memory for the struct
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(result));        
            // Marshall the ApproovTokenFetchResult object to IntPtr
            Marshal.StructureToPtr(result,ptr,false);
            callback(ptr);
        }
        
        // + (nonnull ApproovTokenFetchResult *)fetchApproovTokenAndWait:(nonnull NSString *)url;
        [DllImport("__Internal")]
        private static extern IntPtr Approov_fetchApproovTokenAndWait(string url);
        
        // Define a method to call the native function
        public static ApproovTokenFetchResult FetchApproovTokenAndWait(string url)
        {
            IntPtr resultPtr = Approov_fetchApproovTokenAndWait(url);
            // Convert the NSString pointer to a managed string
            string jsonString = Marshal.PtrToStringAuto(resultPtr);
            // Deserialize the JSON string into a dictionary
            ApproovTokenFetchResult result = JsonUtility.FromJson<ApproovTokenFetchResult>(jsonString);
            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(resultPtr);

            return result;
        }

        // + (void)fetchCustomJWT:(nonnull ApproovTokenFetchCallback)callbackHandler :(nonnull NSString *)payload;
        // Define a method to call the native function
        public static void FetchCustomJWT(Action<IntPtr> callbackHandler, string payload)
        {
            // Call the native function
            IntPtr resultPtr = Approov_fetchCustomJWTAndWait(payload);
            // Convert the NSString pointer to a managed string
            string jsonString = Marshal.PtrToStringAuto(resultPtr);
            // Deserialize the JSON string into a dictionary
            ApproovTokenFetchResult result = JsonUtility.FromJson<ApproovTokenFetchResult>(jsonString);
            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(resultPtr);
            // Allocate unmanaged memory for the struct
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(result));        
            // Marshall the ApproovTokenFetchResult object to IntPtr
            Marshal.StructureToPtr(result,ptr,false);
            callbackHandler(ptr);
        }

        // + (nonnull ApproovTokenFetchResult *)fetchCustomJWTAndWait:(nonnull NSString *)payload;
        [DllImport("__Internal")] 
        private static extern IntPtr Approov_fetchCustomJWTAndWait(string payload);

        // Define a method to call the native function
        public static ApproovTokenFetchResult FetchCustomJWTAndWait(string payload)
        {
            // Call the native function and get the pointer to the result
            IntPtr resultPtr = Approov_fetchCustomJWTAndWait(payload);
            // Convert the NSString pointer to a managed string
            string jsonString = Marshal.PtrToStringAuto(resultPtr);
            // Deserialize the JSON string into a dictionary
            ApproovTokenFetchResult result = JsonUtility.FromJson<ApproovTokenFetchResult>(jsonString);
            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(resultPtr);

            return result;
        }

        // + (void)fetchSecureString:(nonnull ApproovTokenFetchCallback)callbackHandler :(nonnull NSString *)key :(nullable NSString *)newDef;
        // Define a method to call the native function
        public static void FetchSecureString(Action<IntPtr> callbackHandler, string key, string newDef = null)
        {
            // Call the native function
            //Approov_fetchSecureString(callbackHandler, key, newDef);
            // Call the native function
            IntPtr resultPtr = Approov_fetchSecureStringAndWait(key, newDef);
            // Convert the NSString pointer to a managed string
            string jsonString = Marshal.PtrToStringAuto(resultPtr);
            // Deserialize the JSON string into a dictionary
            ApproovTokenFetchResult result = JsonUtility.FromJson<ApproovTokenFetchResult>(jsonString);
            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(resultPtr);
            // Allocate unmanaged memory for the struct
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(result));        
            // Marshall the ApproovTokenFetchResult object to IntPtr
            Marshal.StructureToPtr(result,ptr,false);
            callbackHandler(ptr);
        }

        // + (nonnull ApproovTokenFetchResult *)fetchSecureStringAndWait:(nonnull NSString *)key :(nullable NSString *)newDef;
        [DllImport("__Internal")] // TODO: convert to json
        private static extern IntPtr Approov_fetchSecureStringAndWait(string key, string newDef);

        public static ApproovTokenFetchResult FetchSecureStringAndWait(string key, string newDef = null)
        {
            // Call the native function and get the pointer to the result
            IntPtr resultPtr = Approov_fetchSecureStringAndWait(key, newDef);
            // Convert the NSString pointer to a managed string
            string jsonString = Marshal.PtrToStringAuto(resultPtr);
            // Deserialize the JSON string into a dictionary
            ApproovTokenFetchResult result = JsonUtility.FromJson<ApproovTokenFetchResult>(jsonString);
            // Free the unmanaged memory allocated by the Objective-C function
            Marshal.FreeHGlobal(resultPtr);

            return result;
        }


        // + (void)setUserProperty:(nullable NSString *)property;
        [DllImport("__Internal")] // DONE
        public static extern void Approov_setUserProperty(string property);

        public static void SetUserProperty(string property)
        {
            Approov_setUserProperty(property);
        }

        // + (void)setDevKey:(nullable NSString *)key;
        [DllImport("__Internal")] // DONE
        public static extern void Approov_setDevKey(string key);

        public static void SetDevKey(string key)
        {
            Approov_setDevKey(key);
        }

        // + (void)setDataHashInToken:(nonnull NSString *)data;
        [DllImport("__Internal")] // DONE
        public static extern void Approov_setDataHashInToken(string data);

        public static void SetDataHashInToken(string data)
        {
            Approov_setDataHashInToken(data);
        }

        // + (nullable NSData *)getIntegrityMeasurementProof:(nonnull NSData *)nonce :(nonnull NSData *)measurementConfig;
        [DllImport("__Internal")]
        private static extern IntPtr Approov_getIntegrityMeasurementProof(byte[] nonce, int nonceLength, byte[] measurementConfig, int measurementConfigLength);

        public static byte[] GetIntegrityMeasurementProof(byte[] nonce, byte[] measurementConfig)
        {
            if (nonce == null || measurementConfig == null)
            {
                return null;
            }
            // Call the native function and get the pointer to the result
            IntPtr resultPtr = Approov_getIntegrityMeasurementProof(nonce, nonce.Length, measurementConfig, measurementConfig.Length);
            if (resultPtr == IntPtr.Zero)
            {
                return null;
            }
            // Marshal the pointer to the result structure
            byte[] result = new byte[Marshal.SizeOf(resultPtr)];
            Marshal.Copy(resultPtr, result, 0, result.Length);

            // Free the unmanaged memory allocated by the Objective-C function
            // Note: This step is necessary to prevent memory leaks
            Marshal.FreeHGlobal(resultPtr);

            return result;
        }

        // + (nullable NSData *)getDeviceMeasurementProof:(nonnull NSData *)nonce :(nonnull NSData *)measurementConfig;
        [DllImport("__Internal")] 
        private static extern IntPtr Approov_getDeviceMeasurementProof(byte[] nonce, int nonceLength, byte[] measurementConfig, int measurementConfigLength);

        public static byte[] GetDeviceMeasurementProof(byte[] nonce, byte[] measurementConfig)
        {
            if (nonce == null || measurementConfig == null)
            {
                return null;
            }
            // Call the native function and get the pointer to the result
            IntPtr resultPtr = Approov_getDeviceMeasurementProof(nonce, nonce.Length, measurementConfig, measurementConfig.Length);
            if (resultPtr == IntPtr.Zero)
            {
                return null;
            }
            // Marshal the pointer to the result structure
            byte[] result = new byte[Marshal.SizeOf(resultPtr)];
            Marshal.Copy(resultPtr, result, 0, result.Length);

            // Free the unmanaged memory allocated by the Objective-C function
            // Note: This step is necessary to prevent memory leaks
            Marshal.FreeHGlobal(resultPtr);

            return result;
        }

        // + (nullable NSString *)getDeviceID;
        [DllImport("__Internal")] //DONE
        private static extern IntPtr Approov_getDeviceID();

        public static string GetDeviceID()
        {
            // Call the native function
            IntPtr stringPtr = Approov_getDeviceID();
            // Convert the NSString pointer to a managed string
            string deviceID = Marshal.PtrToStringAuto(stringPtr);

            // Free the unmanaged memory allocated by the Objective-C function
            // Note: This step is necessary to prevent memory leaks
            Marshal.FreeHGlobal(stringPtr);

            return deviceID;
        }

        // + (nullable NSString *)getMessageSignature:(nonnull NSString *)message;
        [DllImport("__Internal")] // DONE
        private static extern IntPtr Approov_getMessageSignature(byte[] message, int messageLength);

        public static string GetMessageSignature(string message)
        {
            // Call the native function
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            IntPtr stringPtr = Approov_getMessageSignature(messageBytes, messageBytes.Length);

            if (stringPtr == IntPtr.Zero)
            {
                return null;
            }
            // Convert the NSString pointer to a managed string
            string signature = Marshal.PtrToStringAuto(stringPtr);

            // Free the unmanaged memory allocated by the Objective-C function
            // Note: This step is necessary to prevent memory leaks
            Marshal.FreeHGlobal(stringPtr);

            return signature;
        }

        //
        [DllImport("__Internal")] // DONE
        private static extern void Approov_emptyGlobalCacheDictionary();

        public static void ClearCertificateCache()
        {
            Approov_emptyGlobalCacheDictionary();
        }

        [DllImport("__Internal")]
        private static extern IntPtr Approov_shouldProceedWithConnection(byte[] cert, int certLength, byte[] hostname, 
                                                                        int hostnameLength, byte[] pinType, int pinTypeLength);

        /* Calls native function shouldProceedWithConnection */
        public static string ShouldProceedWithNetworkConnection(byte[] cert, string url, string pinType) {
            // The cert length
            int certLength = cert.Length;
            // Convert the string to a byte array
            byte[] urlBytes = Encoding.UTF8.GetBytes(url);
            // The length of the byte array
            int urlLength = urlBytes.Length;
            // Convert the string to a byte array
            byte[] pinTypeBytes = Encoding.UTF8.GetBytes(pinType);
            // The length of the byte array
            int pinTypeLength = pinTypeBytes.Length;
            // Call the native function
            IntPtr resultPtr = Approov_shouldProceedWithConnection(cert, certLength, urlBytes, urlLength,pinTypeBytes, pinTypeLength);
            // Convert the NSString pointer to a managed string
            string result = Marshal.PtrToStringAuto(resultPtr);
            if(result == SUCCESS) {
                // "SUCCESS" is returned if the connection is allowed and there was no error in native interface calls
                return null;
            }
            // Free the unmanaged memory allocated by the Objective-C function
            //Marshal.FreeHGlobal(resultPtr);
            return result;
        }
#elif UNITY_ANDROID
        private static IntPtr sApproovClass = IntPtr.Zero;
        // The ApproovTokenFetchResult class
        private static IntPtr sApproovTokenFetchResultClass = IntPtr.Zero;
        /* Dictionary of native java functions in Approov class and their AndroidJNI pointers */
        private static Dictionary<string, IntPtr> sNativeApproovJavaFunctions = new Dictionary<string, IntPtr>();
        /* Dictionary of native java functions in ApproovTokenFetchResult class and their AndroidJNI pointers */
        private static Dictionary<string, IntPtr> sNativeTokenFetchResultJavaFunctions = new Dictionary<string, IntPtr>();

        /* Get the reference to the Approov class */
        public static IntPtr GetApproovClass()
        {
            return sApproovClass;
        }

        /* Get the reference to the ApproovTokenFetchResult class */
        public static IntPtr GetApproovTokenFetchResultClass()
        {
            return sApproovTokenFetchResultClass;
        }

        /* Get the pointer to the native java function belonging to the Approov class */
        public static IntPtr GetNativeApproovJavaFunction(string functionName)
        {
            return sNativeApproovJavaFunctions.GetValueOrDefault(functionName, IntPtr.Zero);
        }

        /* Get the pointer to the native java function belonging to the ApproovTokenFetchResult class */
        public static IntPtr GetNativeTokenFetchResultJavaFunction(string functionName)
        {
            return sNativeTokenFetchResultJavaFunctions.GetValueOrDefault(functionName, IntPtr.Zero);
        }

        /*  Throw an exception with message if the IntPtr is null
        *   @param IntPtr to test for null
        *   @param message to include in the exception
        *   @return void
        */
        public static void CheckIntPtr(IntPtr ptr, string message)
        {
            if (ptr == IntPtr.Zero)
            {
                throw new PermanentException(TAG + message);
            }
        }

        /*  
        *   Initializes the Approov SDK with provided config string
        *   See more info in ApproovService class
        */
        public static void Initialize(string config){
            try
            {
                // Attach the current thread to the JVM
                AndroidJNI.AttachCurrentThread();
                sApproovClass = AndroidJNI.FindClass("com/criticalblue/approovsdk/Approov");
                CheckIntPtr(sApproovClass, "Error: SDK class not found");
                // Get the UnityPlayer activity object and the application context
                IntPtr unityPlayerClass = AndroidJNI.FindClass("com/unity3d/player/UnityPlayer");
                CheckIntPtr(unityPlayerClass, "Error: UnityPlayer class not found");
                // Get the currentActivity field
                IntPtr currentActivityField = AndroidJNI.GetStaticFieldID(unityPlayerClass, "currentActivity", "Landroid/app/Activity;");
                CheckIntPtr(currentActivityField, "Error: currentActivity field not found");
                // Get the current Activity
                IntPtr currentActivity = AndroidJNI.GetStaticObjectField(unityPlayerClass, currentActivityField);
                CheckIntPtr(currentActivity, "Error: currentActivity object not found");
                
                // Get the getApplicationContext method ID from the Activity class
                IntPtr activityClass = AndroidJNI.FindClass("android/app/Activity");
                IntPtr getApplicationContextMethodId = AndroidJNI.GetMethodID(activityClass, "getApplicationContext", "()Landroid/content/Context;");
                CheckIntPtr(getApplicationContextMethodId, "Error: getApplicationContext method not found");

                // Call getApplicationContext
                IntPtr applicationContext = AndroidJNI.CallObjectMethod(currentActivity, getApplicationContextMethodId, new jvalue[0]);
                CheckIntPtr(applicationContext, "Error: Application context is null");

                // Get the initialize method
                IntPtr initializeMethod = AndroidJNI.GetStaticMethodID(sApproovClass, "initialize", "(Landroid/content/Context;Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;)V");
                CheckIntPtr(initializeMethod, "Error: initialize method not found");
                // Prepare the arguments
                jvalue[] args = new jvalue[4];
                args[0] = new jvalue { l = applicationContext };
                args[1] = new jvalue { l = AndroidJNI.NewStringUTF(config) };
                args[2] = new jvalue { l = AndroidJNI.NewStringUTF("auto") };
                args[3] = new jvalue { l = IntPtr.Zero }; // null

                // Call the initialize method
                AndroidJNI.CallStaticVoidMethod(sApproovClass, initializeMethod, args);

                // Clean up
                AndroidJNI.DeleteLocalRef(args[1].l);
                AndroidJNI.DeleteLocalRef(args[2].l);
                //AndroidJNI.DeleteLocalRef(initializeMethod);

                // Now we can find all the native java functions and populate the IntPtrs
                FindNativeJavaFunctions();
            }
            catch (AndroidJavaException e)
            {
                throw new InitializationFailureException(TAG + "Error SDK initialization failed with error: " + e.Message, false);
            }
        } // Initialize

        /* Iterate over the available Approov SDK functions and find their JNI pointers
        *  This funciton gets called once AFTER the SDK has been succesfully initialized
        */
        private static void FindNativeJavaFunctions()
        {
            /* Approov SDK list of functions we need
            "fetchApproovTokenAndWait", "fetchConfig","getDeviceID",
            "fetchSecureStringAndWait", "fetchCustomJWTAndWait",
            "setDataHashInToken", "getMessageSignature", "getPinsJSON","setDevKey",
            "getIntegrityMeasurementProof", "getDeviceMeasurementProof", "setUserProperty",
            "setActivity"
            */
            // Attach the current thread to the JVM
            AndroidJNI.AttachCurrentThread();
            IntPtr methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "fetchApproovTokenAndWait", "(Ljava/lang/String;)Lcom/criticalblue/approovsdk/Approov$TokenFetchResult;");
            CheckIntPtr(methodDef, "Error: fetchApproovTokenAndWait method not found");
            sNativeApproovJavaFunctions.Add("fetchApproovTokenAndWait", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "fetchConfig", "()Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: fetchConfig method not found");
            sNativeApproovJavaFunctions.Add("fetchConfig", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "getDeviceID", "()Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getDeviceID method not found");
            sNativeApproovJavaFunctions.Add("getDeviceID", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "fetchSecureStringAndWait", "(Ljava/lang/String;Ljava/lang/String;)Lcom/criticalblue/approovsdk/Approov$TokenFetchResult;");
            CheckIntPtr(methodDef, "Error: fetchSecureStringAndWait method not found");
            sNativeApproovJavaFunctions.Add("fetchSecureStringAndWait", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "fetchCustomJWTAndWait", "(Ljava/lang/String;)Lcom/criticalblue/approovsdk/Approov$TokenFetchResult;");
            CheckIntPtr(methodDef, "Error: fetchCustomJWTAndWait method not found");
            sNativeApproovJavaFunctions.Add("fetchCustomJWTAndWait", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "setDataHashInToken", "(Ljava/lang/String;)V");
            CheckIntPtr(methodDef, "Error: setDataHashInToken method not found");
            sNativeApproovJavaFunctions.Add("setDataHashInToken", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "getMessageSignature", "(Ljava/lang/String;)Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getMessageSignature method not found");
            sNativeApproovJavaFunctions.Add("getMessageSignature", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "getPinsJSON", "(Ljava/lang/String;)Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getPinsJSON method not found");
            sNativeApproovJavaFunctions.Add("getPinsJSON", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "setDevKey", "(Ljava/lang/String;)V");
            CheckIntPtr(methodDef, "Error: setDevKey method not found");
            sNativeApproovJavaFunctions.Add("setDevKey", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "getIntegrityMeasurementProof", "([B[B)[B");
            CheckIntPtr(methodDef, "Error: getIntegrityMeasurementProof method not found");
            sNativeApproovJavaFunctions.Add("getIntegrityMeasurementProof", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "getDeviceMeasurementProof", "([B[B)[B");
            CheckIntPtr(methodDef, "Error: getDeviceMeasurementProof method not found");
            sNativeApproovJavaFunctions.Add("getDeviceMeasurementProof", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "setUserProperty", "(Ljava/lang/String;)V");
            CheckIntPtr(methodDef, "Error: setUserProperty method not found");
            sNativeApproovJavaFunctions.Add("setUserProperty", methodDef);
            methodDef = AndroidJNI.GetStaticMethodID(sApproovClass, "setActivity", "(Landroid/app/Activity;)V");
            CheckIntPtr(methodDef, "Error: setActivity method not found");
            sNativeApproovJavaFunctions.Add("setActivity", methodDef);
        
            /*  TokenFetchResult class functions:
            "getStatus", "getToken", "getSecureString", "getARC","getRejectionReasons",
            "isConfigChanged", "isForceApplyPins", "getMeasurementConfig", "getLoggableToken"
            */
            sApproovTokenFetchResultClass = AndroidJNI.FindClass("com/criticalblue/approovsdk/Approov$TokenFetchResult");
            CheckIntPtr(sApproovTokenFetchResultClass, "Error: TokenFetchResult class not found");
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "getStatus", "()Lcom/criticalblue/approovsdk/Approov$TokenFetchStatus;");
            CheckIntPtr(methodDef, "Error: getStatus method not found");
            sNativeTokenFetchResultJavaFunctions.Add("getStatus", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "getToken", "()Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getToken method not found");
            sNativeTokenFetchResultJavaFunctions.Add("getToken", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "getSecureString", "()Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getSecureString method not found");
            sNativeTokenFetchResultJavaFunctions.Add("getSecureString", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "getARC", "()Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getARC method not found");
            sNativeTokenFetchResultJavaFunctions.Add("getARC", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "getRejectionReasons", "()Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getRejectionReasons method not found");
            sNativeTokenFetchResultJavaFunctions.Add("getRejectionReasons", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "isConfigChanged", "()Z");
            CheckIntPtr(methodDef, "Error: isConfigChanged method not found");
            sNativeTokenFetchResultJavaFunctions.Add("isConfigChanged", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "isForceApplyPins", "()Z");
            CheckIntPtr(methodDef, "Error: isForceApplyPins method not found");
            sNativeTokenFetchResultJavaFunctions.Add("isForceApplyPins", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "getMeasurementConfig", "()[B");
            CheckIntPtr(methodDef, "Error: getMeasurementConfig method not found");
            sNativeTokenFetchResultJavaFunctions.Add("getMeasurementConfig", methodDef);
            methodDef = AndroidJNI.GetMethodID(sApproovTokenFetchResultClass, "getLoggableToken", "()Ljava/lang/String;");
            CheckIntPtr(methodDef, "Error: getLoggableToken method not found");
            sNativeTokenFetchResultJavaFunctions.Add("getLoggableToken", methodDef);
            //AndroidJNI.DeleteLocalRef(methodDef);
        }// FindNativeJavaFunctions

        // Auxiliary function converting java TokenFetchStatus to ApproovTokenFetchStatus
        public static ApproovTokenFetchStatus ConvertTokenFetchStatus(int status)
        {
            switch (status)
            {
                
                case 0:
                    return ApproovTokenFetchStatus.Success;
                case 1:
                    return ApproovTokenFetchStatus.NoNetwork;
                case 2:
                    return ApproovTokenFetchStatus.MITMDetected;
                case 3:
                    return ApproovTokenFetchStatus.PoorNetwork;
                case 4:
                    return ApproovTokenFetchStatus.NoApproovService;
                case 5:
                    return ApproovTokenFetchStatus.BadURL;
                case 6:
                    return ApproovTokenFetchStatus.UnknownURL;
                case 7:
                    return ApproovTokenFetchStatus.UnprotectedURL;
                case 8:
                    return ApproovTokenFetchStatus.NoNetworkPermission;
                case 9:
                    return ApproovTokenFetchStatus.MissingLibDependency;
                case 10:
                    return ApproovTokenFetchStatus.InternalError;
                case 11:
                    return ApproovTokenFetchStatus.Rejected;
                case 12:
                    return ApproovTokenFetchStatus.Disabled;
                case 13:
                    return ApproovTokenFetchStatus.UnknownKey;
                case 14:
                    return ApproovTokenFetchStatus.BadKey;  // to shut up the compiler
                case 15:
                    return ApproovTokenFetchStatus.BadPayload; // to shut up the compiler
                default:
                    return ApproovTokenFetchStatus.InternalError; // to shut up the compiler
            }
        }
        /*  Sets a user property in the SDK. See more info in ApproovService class */
        public static void SetUserProperty(string property)  {
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(property) };
            AndroidJNI.CallStaticVoidMethod(sApproovClass, sNativeApproovJavaFunctions["setUserProperty"], args);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
        }
        /*  Sets activity to bind to in the SDK. See more info in ApproovService class */
        public static void SetActivity(AndroidJavaObject activity) {
            //sApproovClass.CallStatic("SetActivity", activity);
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            args[0] = new jvalue { l = activity.GetRawObject() };
            AndroidJNI.CallStaticVoidMethod(sApproovClass, sNativeApproovJavaFunctions["setActivity"], args);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
        }
        /*  Fetches a secure string. See more info in ApproovService class */
        public static ApproovTokenFetchResult FetchSecureStringAndWait(string key, string newDef) {
            // Prepare the arguments
            jvalue[] args = new jvalue[2];
            // Invoke fetchSecureString
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(key) };
            if (newDef == null) {
                args[1] = new jvalue { l = IntPtr.Zero };
            } else {
                args[1] = new jvalue { l = AndroidJNI.NewStringUTF(newDef) };
            }
            
            IntPtr tokenFetchResultPtr = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["fetchSecureStringAndWait"], args);
            if (tokenFetchResultPtr == IntPtr.Zero)
            {
                ApproovTokenFetchResult failureResult = new ApproovTokenFetchResult();
                failureResult.status = ApproovTokenFetchStatus.InternalError;
                return failureResult;
            }
            // We have to convert this to TokenFetchResult structure and return it
            ApproovTokenFetchResult result = new ApproovTokenFetchResult();
            IntPtr statusEnumObject = AndroidJNI.CallObjectMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getStatus"], new jvalue[0]);
            // Get the ordinal method ID
            IntPtr enumClass = AndroidJNI.FindClass("java/lang/Enum");
            IntPtr ordinalMethodId = AndroidJNI.GetMethodID(enumClass, "ordinal", "()I");
            // Call ordinal
            int tempStatus = AndroidJNI.CallIntMethod(statusEnumObject, ordinalMethodId, new jvalue[0]);
            result.status = ConvertTokenFetchStatus(tempStatus);
            result.ARC = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getARC"], new jvalue[0]);
            result.isForceApplyPins = AndroidJNI.CallBooleanMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["isForceApplyPins"], new jvalue[0]);
            result.token = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getToken"], new jvalue[0]);
            result.rejectionReasons = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getRejectionReasons"], new jvalue[0]);
            result.isConfigChanged = AndroidJNI.CallBooleanMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["isConfigChanged"], new jvalue[0]);
            IntPtr resultObject = AndroidJNI.CallObjectMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getMeasurementConfig"], new jvalue[0]);
            if (resultObject == IntPtr.Zero)
            {
                result.measurementConfig = null;
            } else {
                byte[] resultArray = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(resultObject);
                result.measurementConfig = resultArray;
                AndroidJNI.DeleteLocalRef(resultObject);
            }
            result.secureString = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getSecureString"], new jvalue[0]);
            result.loggableToken = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getLoggableToken"], new jvalue[0]);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
            AndroidJNI.DeleteLocalRef(args[1].l);
            AndroidJNI.DeleteLocalRef(tokenFetchResultPtr);
            return result;
        }
        /*  Fetches a custom JWT. See more info in ApproovService class */
        public static ApproovTokenFetchResult FetchCustomJWTAndWait(string payload) {
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            // Invoke fetchCustomJWT
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(payload) };
            IntPtr tokenFetchResultPtr = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["fetchCustomJWTAndWait"], args);
            if (tokenFetchResultPtr == IntPtr.Zero)
            {
                ApproovTokenFetchResult failureResult = new ApproovTokenFetchResult();
                failureResult.status = ApproovTokenFetchStatus.InternalError;
                return failureResult;
            }
            // We have to convert this to TokenFetchResult structure and return it
            ApproovTokenFetchResult result = new ApproovTokenFetchResult();
            IntPtr statusEnumObject = AndroidJNI.CallObjectMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getStatus"], new jvalue[0]);
            // Get the ordinal method ID
            IntPtr enumClass = AndroidJNI.FindClass("java/lang/Enum");
            IntPtr ordinalMethodId = AndroidJNI.GetMethodID(enumClass, "ordinal", "()I");
            // Call ordinal
            int tempStatus = AndroidJNI.CallIntMethod(statusEnumObject, ordinalMethodId, new jvalue[0]);
            result.status = ConvertTokenFetchStatus(tempStatus);
            result.ARC = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getARC"], new jvalue[0]);
            result.isForceApplyPins = AndroidJNI.CallBooleanMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["isForceApplyPins"], new jvalue[0]);
            result.token = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getToken"], new jvalue[0]);
            result.rejectionReasons = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getRejectionReasons"], new jvalue[0]);
            IntPtr resultObject = AndroidJNI.CallObjectMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getMeasurementConfig"], new jvalue[0]);
            if (resultObject == IntPtr.Zero)
            {
                result.measurementConfig = null;
            } else {
                byte[] resultArray = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(resultObject);
                result.measurementConfig = resultArray;
                AndroidJNI.DeleteLocalRef(resultObject);
            }
            result.isConfigChanged = AndroidJNI.CallBooleanMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["isConfigChanged"], new jvalue[0]);
            result.secureString = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getSecureString"], new jvalue[0]);
            result.loggableToken = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getLoggableToken"], new jvalue[0]);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
            AndroidJNI.DeleteLocalRef(tokenFetchResultPtr);
            return result;
        }
        /*  Gets device ID string string. See more info in ApproovService class */
        public static string GetDeviceID() {
            IntPtr deviceIDString = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["getDeviceID"], new jvalue[0]);
            if(deviceIDString == IntPtr.Zero)
            {
                return null;
            }
            // Convert the result to a C# string
            string deviceID = AndroidJNI.GetStringUTFChars(deviceIDString);
            // Cleanup
            AndroidJNI.DeleteLocalRef(deviceIDString);
            return deviceID;
        }
        /*  Sets a data hash in token. See more info in ApproovService class */
        public static void SetDataHashInToken(string data) {
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(data) };
            AndroidJNI.CallStaticVoidMethod(sApproovClass, sNativeApproovJavaFunctions["setDataHashInToken"], args);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
        }
        /*  Gets message signature. See more info in ApproovService class */
        public static string GetMessageSignature(string message) {
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(message) };
            IntPtr signatureString = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["getMessageSignature"], args);
            if (signatureString == IntPtr.Zero)
            {
                AndroidJNI.DeleteLocalRef(args[0].l);
                return null;
            }
            // Convert the result to a C# string
            string signature = AndroidJNI.GetStringUTFChars(signatureString);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
            AndroidJNI.DeleteLocalRef(signatureString);
            return signature;
        }
        /*  Fetches an Approov token. See more info in ApproovService class */
        public static ApproovTokenFetchResult FetchApproovTokenAndWait(string url) {
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(url) };
            IntPtr tokenFetchResultPtr = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["fetchApproovTokenAndWait"], args);
            if(tokenFetchResultPtr == IntPtr.Zero)
            {
                // This should not happen, but if for some reason AndroidJNI.CallStaticObjectMethod returns null
                AndroidJNI.DeleteLocalRef(args[0].l);
                ApproovTokenFetchResult resultEmpty = new ApproovTokenFetchResult();
                resultEmpty.status = ApproovTokenFetchStatus.InternalError;
                return resultEmpty;
            }
            // We have to convert this to TokenFetchResult structure and return it
            ApproovTokenFetchResult result = new ApproovTokenFetchResult();
            IntPtr statusEnumObject = AndroidJNI.CallObjectMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getStatus"], new jvalue[0]);
            // Get the ordinal method ID
            IntPtr enumClass = AndroidJNI.FindClass("java/lang/Enum");
            IntPtr ordinalMethodId = AndroidJNI.GetMethodID(enumClass, "ordinal", "()I");
            // Call ordinal
            int tempStatus = AndroidJNI.CallIntMethod(statusEnumObject, ordinalMethodId, new jvalue[0]);
            result.status = ConvertTokenFetchStatus(tempStatus);
            result.ARC = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getARC"], new jvalue[0]);
            result.isForceApplyPins = AndroidJNI.CallBooleanMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["isForceApplyPins"], new jvalue[0]);
            result.token = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getToken"], new jvalue[0]);
            result.rejectionReasons = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getRejectionReasons"], new jvalue[0]);
            IntPtr resultObject = AndroidJNI.CallObjectMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getMeasurementConfig"], new jvalue[0]);
            if (resultObject == IntPtr.Zero)
            {
                result.measurementConfig = null;
            } else {
                byte[] resultArray = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(resultObject);
                result.measurementConfig = resultArray;
                AndroidJNI.DeleteLocalRef(resultObject);
            }
            result.isConfigChanged = AndroidJNI.CallBooleanMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["isConfigChanged"], new jvalue[0]);
            result.secureString = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getSecureString"], new jvalue[0]);
            result.loggableToken = AndroidJNI.CallStringMethod(tokenFetchResultPtr, sNativeTokenFetchResultJavaFunctions["getLoggableToken"], new jvalue[0]);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
            AndroidJNI.DeleteLocalRef(tokenFetchResultPtr);
            return result;
        }
        /*  Gets the pins JSON. See more info in ApproovService class */
        public static string GetPinsJSON(string pinType) {
            Console.WriteLine(TAG + "Getting pins for pinType: " + pinType);
            if (pinType == null) {
                throw new ArgumentNullException("Error: pinType is null");
            }
            // Attach current thread to JVM
            int attachResult = AndroidJNI.AttachCurrentThread();
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(pinType) };
            IntPtr pinsString = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["getPinsJSON"], args);
            if(pinsString == IntPtr.Zero)
            {
                AndroidJNI.DeleteLocalRef(args[0].l);
                return null;
            }
            // Convert the result to a C# string
            string pins = AndroidJNI.GetStringUTFChars(pinsString);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
            AndroidJNI.DeleteLocalRef(pinsString);
            return pins;
        }
        /*  Fetches the Approov configuration. See more info in ApproovService class */
        public static string FetchConfig() {
            IntPtr configString = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["fetchConfig"], new jvalue[0]);
            if(configString == IntPtr.Zero)
            {
                return null;
            }
            // Convert the result to a C# string
            string config = AndroidJNI.GetStringUTFChars(configString);
            // Cleanup
            AndroidJNI.DeleteLocalRef(configString);
            return config;
        }
        /*  Sets the dev key. See more info in ApproovService class */
        public static void SetDevKey(string key) {
            if (key == null) {
                throw new ArgumentNullException("Error: key is null");
            }
            // Prepare the arguments
            jvalue[] args = new jvalue[1];
            args[0] = new jvalue { l = AndroidJNI.NewStringUTF(key) };
            AndroidJNI.CallStaticVoidMethod(sApproovClass, sNativeApproovJavaFunctions["setDevKey"], args);
            // Cleanup
            AndroidJNI.DeleteLocalRef(args[0].l);
        }
        /*  Gets the integrity measurement proof. See more info in ApproovService class */
        public static byte[] GetIntegrityMeasurementProof(byte[] nonce, byte[] measurementConfig) {
            // Prepare the arguments
            jvalue[] args = new jvalue[2];
            IntPtr noncePtr = AndroidJNIHelper.ConvertToJNIArray(nonce);
            IntPtr measurementConfigPtr = AndroidJNIHelper.ConvertToJNIArray(measurementConfig);
            args[0] = new jvalue { l = noncePtr };
            args[1] = new jvalue { l = measurementConfigPtr };
            // Invoke the native method
            IntPtr proof = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["getIntegrityMeasurementProof"], args);
            byte[] proofBytes = null;
            if(proof != IntPtr.Zero) {
                // Convert the result to a C# byte array
                sbyte[] proofSBytes = AndroidJNI.FromSByteArray(proof);
                // Convert to byte[] from sbyte[]
                byte[] temp = Array.ConvertAll(proofSBytes, b => (byte)b);
                proofBytes = new byte[temp.Length];
                Array.Copy(temp, proofBytes, temp.Length);
                // Cleanup
                AndroidJNI.DeleteLocalRef(proof);
            }
            // Cleanup
            AndroidJNI.DeleteLocalRef(noncePtr);
            AndroidJNI.DeleteLocalRef(measurementConfigPtr);
            return proofBytes;
        }
        /*  Gets the device measurement proof. See more info in ApproovService class */
        public static byte[] GetDeviceMeasurementProof(byte[] nonce, byte[] measurementConfig) {
            // Prepare the arguments
            jvalue[] args = new jvalue[2];
            IntPtr noncePtr = AndroidJNIHelper.ConvertToJNIArray(nonce);
            IntPtr measurementConfigPtr = AndroidJNIHelper.ConvertToJNIArray(measurementConfig);
            args[0] = new jvalue { l = noncePtr };
            args[1] = new jvalue { l = measurementConfigPtr };
            // Invoke the native method
            IntPtr proof = AndroidJNI.CallStaticObjectMethod(sApproovClass, sNativeApproovJavaFunctions["getDeviceMeasurementProof"], args);
            byte[] proofBytes = null;
            if(proof != IntPtr.Zero) {
                // Convert the result to a C# byte array
                sbyte[] proofSBytes = AndroidJNI.FromSByteArray(proof);
                // Convert to byte[] from sbyte[]
                byte[] temp = Array.ConvertAll(proofSBytes, b => (byte)b);
                proofBytes = new byte[temp.Length];
                Array.Copy(temp, proofBytes, temp.Length);
                // Cleanup
                AndroidJNI.DeleteLocalRef(proof);
            }
            // Cleanup
            AndroidJNI.DeleteLocalRef(noncePtr);
            AndroidJNI.DeleteLocalRef(measurementConfigPtr);
            return proofBytes;
        }

        /* Clears the certificate cache in the native layer */
        public static void ClearCertificateCache() {
            // Attach current thread to JVM
            AndroidJNI.AttachCurrentThread();
            // Get java class: io.approov.approovsdk.unity.ApproovUnity
            IntPtr approovUnityClass = AndroidJNI.FindClass("io/approov/approovsdk/unity/ApproovUnity");
            CheckIntPtr(approovUnityClass, "Error: ApproovUnity class not found");
            // Call the clearCertificateCache method
            IntPtr clearCertificateCacheMethod = AndroidJNI.GetStaticMethodID(approovUnityClass, "clearCertCache", "()V");
            // Prepare the arguments
            jvalue[] args = new jvalue[0];
            AndroidJNI.CallStaticVoidMethod(approovUnityClass, clearCertificateCacheMethod, args);
        }

        /** Calls native function shouldProceedWithConnection: Note the url passed to this function
        *   should only contain the domain part of the URL, not the full URL.
        */
        public static string ShouldProceedWithNetworkConnection(byte[] cert, string domain, string pinType) {
            // Attach current thread to JVM
            AndroidJNI.AttachCurrentThread();
            // Get java class: io.approov.approovsdk.unity.ApproovUnity
            IntPtr approovUnityClass = AndroidJNI.FindClass("io/approov/approovsdk/unity/ApproovUnity");
            CheckIntPtr(approovUnityClass, "Error: ApproovUnity class not found");
            // Call the shouldProceedWithNetworkConnection method
            IntPtr shouldProceedWithNetworkConnectionMethod = AndroidJNI.GetStaticMethodID(approovUnityClass, "shouldProceedWithConnection", "([BLjava/lang/String;Ljava/lang/String;)Ljava/lang/String;");
            CheckIntPtr(shouldProceedWithNetworkConnectionMethod, "Error: shouldProceedWithConnection method not found");
            // Prepare the arguments
            jvalue[] args = new jvalue[3];
            args[0] = new jvalue { l = AndroidJNIHelper.ConvertToJNIArray(cert) };
            args[1] = new jvalue { l = AndroidJNI.NewStringUTF(domain) };
            args[2] = new jvalue { l = AndroidJNI.NewStringUTF(pinType) };
            // Call the method
            IntPtr result = AndroidJNI.CallStaticObjectMethod(approovUnityClass, shouldProceedWithNetworkConnectionMethod, args);
            if (result == IntPtr.Zero)
            {
                // Cleanup
                AndroidJNI.DeleteLocalRef(args[0].l);
                AndroidJNI.DeleteLocalRef(args[1].l);
                AndroidJNI.DeleteLocalRef(args[2].l);
                return TAG + "Error: AndroidJNI call to shouldProceedWithConnection returned null";
            }
            
            // Convert pointer to C# string
            string resultString = AndroidJNI.GetStringUTFChars(result);
            if (resultString == null)
            {
                // AndroidJNI issue during call to string conversion
                resultString = TAG + "Error: AndroidJNI GetStringUTFChars returned null";
            } else if (resultString == SUCCESS )
            {
                // This means native interface has returned success
                resultString = null;
            }

            // Cleanup
            AndroidJNI.DeleteLocalRef(result);
            AndroidJNI.DeleteLocalRef(args[0].l);
            AndroidJNI.DeleteLocalRef(args[1].l);
            AndroidJNI.DeleteLocalRef(args[2].l);
            // Return result which could be null on success or contain an error message
            return resultString;
        }
#endif
    } // ApproovBridge class
    
}// namespace Approov
