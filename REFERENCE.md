# Reference

The Unity support from Approov consists of platform dependent code, included in the `ApproovUnity.java` and `ApproovBridge-ObjectiveC.mm` files. The C# classes are platform independent and include the following classes: `ApproovCertificateHandler` handles pinning and certificate related operations; `ApproovException` handles exceptions and is subclassed with classes like `ConfigurationFailureException` and `PinningErrorException` which indicate further the cause of the actual exception; the `ApproovWebRequest` subclasses `UnityWebRequest` class by including the Approov SDK support.

The `ApproovService` and the `ApproovBridge` classes both provide interfaces to the underlying platform-dependent binary SDK, but they differ in their levels of abstraction and ease of use. The `ApproovBridge` class operates at a lower level, offering more granular control and requiring more detailed handling. In contrast, the `ApproovService` class abstracts many of these details, providing a simpler and more user-friendly interface. Both classes will be described in more detail below. These classes are available if you import the namespace into your project source file:

```C#
using Approov;
```

## APPROOV SERVICE API

## Initialize
Initializes the Approov SDK and thus enables the Approov features. The `config` will have been provided in the initial onboarding or email or can be [obtained](https://approov.io/docs/latest/approov-usage-documentation/#getting-the-initial-sdk-configuration) using the approov CLI. This will generate an error if a second attempt is made at initialization with a different `config`.

```C#
void Initialize(String config)
```

## IsSDKInitialized
Returns true if the native SDK has been initialized

```C#
public static bool IsSDKInitialized()
```

## SetBindingHeader / GetBindingHeader
Sets a binding `header` that may be present on requests being made. This is for the [token binding](https://approov.io/docs/latest/approov-usage-documentation/#token-binding) feature. A header should be chosen whose value is unchanging for most requests (such as an Authorization header). If the `header` is present, then a hash of the `header` value is included in the issued Approov tokens to bind them to the value. This may then be verified by the backend API integration.

```C#
public static void SetBindingHeader(string header);
public static string GetBindingHeader();
```

## SetTokenHeaderAndPrefix / GetTokenHeader / GetTokenPrefix
Sets or gets the `header` that the Approov token is added on, as well as an optional `prefix` String (such as "`Bearer `"). Set `prefix` to the empty string if it is not required. By default the token is provided on `Approov-Token` with no prefix.

```C#
public static void SetTokenHeaderAndPrefix(string header, string prefix);
public static string GetTokenHeader();
public static string GetTokenPrefix();
```

## SetProceedOnNetworkFailure / GetProceedOnNetworkFailure
If the provided `proceed` value is `true` then this indicates that the network interceptor should proceed anyway if it is not possible to obtain an Approov token due to a networking failure. If this is called then the backend API can receive calls without the expected Approov token header being added, or without header/query parameter substitutions being made. This should only ever be used if there is some particular reason, perhaps due to local network conditions, that you believe that traffic to the Approov cloud service will be particularly problematic.

```C#
public static void SetProceedOnNetworkFailure(bool proceed);
public static bool GetProceedOnNetworkFailure();
```

## AddSubstitutionHeader / RemoveSubstitutionHeader / GetSubstitutionHeaders
Adds the name of a `header` which should be subject to [secure strings](https://approov.io/docs/latest/approov-usage-documentation/#secure-strings) substitution. This means that if the `header` is present then the value will be used as a key to look up a secure string value which will be substituted into the `header` value instead. This allows easy migration to the use of secure strings. A `requiredPrefix` may be specified to deal with cases such as the use of "`Bearer `" prefixed before values in an authorization header. Set `requiredPrefix` to `null` if it is not required.

```C#
public static void AddSubstitutionHeader(string header, string requiredPrefix);
public static void RemoveSubstitutionHeader(string header);
public static Dictionary<string, string> GetSubstitutionHeaders();
```

## AddExclusionURLRegex / RemoveExclusionURLRegex / CheckURLIsExcluded
Adds an exclusion URL [regular expression](https://regex101.com/) via the `urlRegex` parameter. If a URL for a request matches this regular expression then it will not be subject to any Approov protection.

```C#
public static void AddExclusionURLRegex(string urlRegex);
public static void RemoveExclusionURLRegex(string urlRegex);
public static bool CheckURLIsExcluded(string url);
```

## AddSubstitutionQueryParam / RemoveSubstitutionQueryParam / GetSubstitutionQueryParams
Adds a `key` name for a query parameter that should be subject to [secure strings](https://approov.io/docs/latest/approov-usage-documentation/#secure-strings) substitution. This means that if the query parameter is present in a URL then the value will be used as a key to look up a secure string value which will be substituted as the query parameter value instead. This allows easy migration to the use of secure strings.

```C#
public static void AddSubstitutionQueryParam(string key);
public static void RemoveSubstitutionQueryParam(string key);
public static HashSet<string> GetSubstitutionQueryParams();
```

## SetUserProperty
Sets a user defined property on the SDK. This may provide information about the app state or aspects of the environment it is running in.

```C#
public static void SetUserProperty(string property)
```

## SetActivity (Android ONLY)
Sets the information about a current activity. This may be set for an expected app launch activity so that analysis can be performed to determine if the activity may have been launched in an automatic way.

```C#
public static void SetActivity(AndroidJavaObject activity)
```

Note that the activity can be obtained in Unite like so:

```C#
AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
ApproovService.SetActivity(currentActivity);
```

## Prefetch
Allows token prefetch operation to be performed as early as possible. This permits a token to be available while an application might be loading resources or is awaiting user input.

```C#
public static void Prefetch()
```

## FetchSecureString
Fetches a [secure string](https://approov.io/docs/latest/approov-usage-documentation/#secure-strings) with the given `key` if `newDef` is `null`. Throws an exception if the `key` secure string is not defined. If `newDef` is not `null` then a secure string for the particular app instance may be defined. In this case the new value is returned as the secure string. Use of an empty string for `newDef` removes the string entry. Note that the returned string should NEVER be cached by your app, you should call this function when it is needed.

```C#
public static string FetchSecureString(string key, string newDef)
```

This operation may require network access so may take some time to complete, and should not be called from the UI thread.

## FetchCustomJWT
Fetches a [custom JWT](https://approov.io/docs/latest/approov-usage-documentation/#custom-jwts) with the given marshaled JSON `payload`.

```C#
public static string FetchCustomJWT(string payload)
```

This throws an exception if there was a problem obtaining the custom JWT. This may require network access so may take some time to complete, and should not be called from the UI thread.

## Precheck
Performs a precheck to determine if the app will pass attestation. This requires [secure strings](https://approov.io/docs/latest/approov-usage-documentation/#secure-strings) to be enabled for the account, although no strings need to be set up. 

```C#
public static void Precheck()
```

This throws an exception if the precheck failed. This will likely require network access so may take some time to complete, and should not be called from the UI thread.

## GetDeviceID
Gets the [device ID](https://approov.io/docs/latest/approov-usage-documentation/#extracting-the-device-id) used by Approov to identify the particular device that the SDK is running on. Note that different Approov apps on the same device will return a different ID. Moreover, the ID may be changed by an uninstall and reinstall of the app.

```C#
public static string GetDeviceID()
```

## SetDataHashInToken
Directly sets the [token binding](https://approov.io/docs/latest/approov-usage-documentation/#token-binding) hash to be included in subsequently fetched Approov tokens. If the hash is different from any previously set value then this will cause the next token fetch operation to fetch a new token with the correct payload data hash. The hash appears in the `pay` claim of the Approov token as a base64 encoded string of the SHA256 hash of the data. Note that the data is hashed locally and never sent to the Approov cloud service. This is an alternative to using `SetBindingHeader` and you should not use both methods at the same time.

```C#
public static void SetDataHashInToken(String data)
```

## GetMessageSignature
Gets the [message signature](https://approov.io/docs/latest/approov-usage-documentation/#message-signing) for the given `message`. This is returned as a base64 encoded signature. This feature uses an account specific message signing key that is transmitted to the SDK after a successful fetch if the facility is enabled for the account. Note that if the attestation failed then the signing key provided is actually random so that the signature will be incorrect. An Approov token should always be included in the message being signed and sent alongside this signature to prevent replay attacks.

```C#
public static string GetMessageSignature(string message)
```

## FetchToken
Performs an Approov token fetch for the given `url`. This should be used in situations where it is not possible to use the networking interception to add the token. Note that the returned token should NEVER be cached by your app, you should call this function when it is needed.

```C#
public static string FetchToken(string url)
```

This throws an exception if there was a problem obtaining an Approov token. This may require network access so may take some time to complete, and should not be called from the UI thread.

## GetPinsJSON
Get set of pins from Approov SDK in JSON format

```C#
public static string GetPinsJSON(string pinType)
```

## FetchConfig
Fetches the current configuration for the SDK. This may be the initial configuration or may be a new updated configuration returned from the Approov cloud service. Normally this method returns the latest configuration that is available and is cached in the SDK. Thus the method will return quickly. However, if this method is called when there has been no prior call to fetch an Approov token then a network request to the Approov cloud service will be made to obtain any latest configuration update.

```C#
public static string FetchConfig()
```

## SetDevKey
[Sets a development key](https://approov.io/docs/latest/approov-usage-documentation/#using-a-development-key) in order to force an app to be passed. This can be used if the app has to be resigned in a test environment and would thus fail attestation otherwise.

```C#
public static void SetDevKey(string key)
```

## GetIntegrityMeasurementProof
Obtains an integrity measurement proof that is used to show that the app and its environment have not changed since the time of the original integrity measurement. The proof does an HMAC calculation over the secret integrity measurement value which is salted by a provided nonce. This proves that the SDK is able to reproduce the integrity measurement value.

```C#
public static byte[] GetIntegrityMeasurementProof(byte[] nonce, byte[] measurementConfig)
```

## GetDeviceMeasurementProof
Obtains a device measurement proof that is used to show that the device environment has not changed since the time of the original integrity measurement. This allows the app version, including the Approov SDK, to be updated while preserving the device measurement. The proof does an HMAC calculation over the secret device measurement value which is salted by a provided nonce. This proves that the SDK is able to reproduce the device measurement value.

```C#
public static byte[] GetDeviceMeasurementProof(byte[] nonce, byte[] measurementConfig)
```

## APPROOV BRIDGE API

## ApproovTokenFetchResult
Describes a token fetch/secure string/customJWT fetch result.

```C#
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
```

## StringFromApproovTokenFetchStatus (IOS ONLY)
Utility function that converts `ApproovTokenFetchStatus` enum to its string representation

```C#
public static string StringFromApproovTokenFetchStatus(ApproovTokenFetchStatus approovTokenFetchStatus)
```

## FetchConfig
See `ApproovService` description

```C#
public static string FetchConfig()
```


## GetPinsJSON
See `ApproovService` description

```C#
public static string GetPinsJSON(string pinType)
```


## Initialize
See `ApproovService` description

```C#
public static bool Initialize(string initialConfig, string updateConfig, string comment, out IntPtr error)
```

## FetchApproovToken - IOS ONLY
This is the async version of the `FetchApproovTokenAndWait` function and upon completion, performs a callback with a single structure pointer as argument of type `ApproovTokenFetchResult`.

```C#
public static void FetchApproovToken(Action<IntPtr> callback, string url)
```

## FetchApproovTokenAndWait
See `ApproovService` description. This is the same function as in `ApproovService` but instead of returning the actual token, it returns an object of type `ApproovTokenFetchResult`.

```C#
public static ApproovTokenFetchResult FetchApproovTokenAndWait(string url)
```

## FetchCustomJWT - IOS ONLY
Async version of the `FetchCustomJWTAndWait` function. Upon completion, the callback handler is invoked with a single argument of type `ApproovTokenFetchResult`.

```C#
public static void FetchCustomJWT(Action<IntPtr> callbackHandler, string payload)
```

## FetchCustomJWTAndWait
See `ApproovService` description. This is the same function as in `ApproovService` but instead of returning the actual token, it returns an object of type `ApproovTokenFetchResult`.

```C#
public static ApproovTokenFetchResult FetchCustomJWTAndWait(string payload)
```

## FetchSecureString - IOS ONLY
Async version of the `FetchSecureStringANdWait` function. Upon completion, the callback handler is invoked with a single argument of type `ApproovTokenFetchResult`.

```C#
public static void FetchSecureString(Action<IntPtr> callbackHandler, string key, string newDef = null)
```

## FetchSecureStringANdWait
See `ApproovService` description. This is the same function as in `ApproovService` but instead of returning the actual token, it returns an object of type `ApproovTokenFetchResult`.
```C#
public static ApproovTokenFetchResult FetchSecureStringAndWait(string key, string newDef = null)
```

## SetProperty
See `ApproovService` description.

```C#
public static void SetUserProperty(string property)
```

## SetDevKey
See `ApproovService` description.

```C#
public static void SetDevKey(string key)
```


## SetDataHashInToken
See `ApproovService` description.

```C#
public static void SetDataHashInToken(string data)
```


## GetIntegrityMeasurementProof
See `ApproovService` description.

```C#
public static byte[] GetIntegrityMeasurementProof(byte[] nonce, byte[] measurementConfig)
```

## GetDeviceMeasurementProof
See `ApproovService` description.

```C#
public static byte[] GetDeviceMeasurementProof(byte[] nonce, byte[] measurementConfig)
```


## GetDeviceID
See `ApproovService` description.

```C#
public static string GetDeviceID()
```

## GetMessageSignature
See `ApproovService` description.

```C#
public static string GetMessageSignature(string message)
```


## ClearCertificateCache
Clears the certificate cache in the native layer. The cache contains certificates from domains that have been verified but it gets automatically cleared if a different certificate is presented for same domain or there is an error in obtaining current certificate chain for a domain.

```C#
public static void ClearCertificateCache()
```


## ShouldProceedWithNetworkConnection
Function invoked to decide if a netwok connection to a host should proceed or not. In order to proceed, if the host is pinned in Approov, the requirement is the pins defined in Approov cloud must match current pin from the connection. If there is a pin match, we verify the certificate chain is valid and proceed in case it is. If the host is not pinned, we still verify the certificate chain for validaity and proceed with connection in the case the chain is valid.
Note that this function calls the native layer and depends on the implementation of UnityWebRequest certficate handler class which only provides the leaf certificate for validation as oposite to the full certificate chain. This function is called from the ApproovCertificateHandler class VerifyCertificate function only.

```C#
public static string ShouldProceedWithNetworkConnection(byte[] cert, string url, string pinType)
```


## ConvertTokenFetchStatus - ANDROID ONLY
Auxiliary function converting java TokenFetchStatus to ApproovTokenFetchStatus representation in C#

```C#
public static ApproovTokenFetchStatus ConvertTokenFetchStatus(int status)
```

## SetActivity - ANDROID ONLY
Sets the information about a current activity. This may be set for an expected app launch activity so that analysis can be performed to determine if the activity may have been launched in an automatic way.

```C#
public static void SetActivity(AndroidJavaObject activity)
```