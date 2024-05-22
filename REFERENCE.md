# Reference

The Unity support from Approov consists of platform dependent code, included in the `ApproovUnity.java` and `ApproovBridge-ObjectiveC.mm` files. The C# classes are platform independent and include the following classes: `ApproovCertificateHandler` handles pinning and certificate related operations; `ApproovException` handles exceptions and is subclassed with classes like `ConfigurationFailureException` and `PinningErrorException` which indicate further the cause of the actual exception; the `ApproovWebRequest` subclasses `UnityWebRequest` class by including the Approov SDK support.

The `ApproovService` and the `ApproovBridge` classes both provide interfaces to the underlying platform-dependent binary SDK, but they differ in their levels of abstraction and ease of use. The `ApproovBridge` class operates at a lower level, offering more granular control and requiring more detailed handling. In contrast, the `ApproovService` class abstracts many of these details, providing a simpler and more user-friendly interface. Both classes will be described in more detail below. These classes are available if you import the namespace into your project source file:

```C#
using Approov;
```

## APPROOV SERVICE

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

