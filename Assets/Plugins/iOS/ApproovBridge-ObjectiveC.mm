// Objective-C++ wrapper class
#import "Approov/Approov.h"
#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <CommonCrypto/CommonDigest.h>

// The logging tag
NSString *TAG = @"ApproovBridge: ";
// Success string for C# code
NSString *SUCCESS = @"SUCCESS";
// Timeout in seconds for a getting the host certificates
static const NSTimeInterval FETCH_CERTIFICATES_TIMEOUT = 3;

// Certificate harvesting/validation class
@interface CertificateHandler : NSObject <NSURLSessionTaskDelegate>
// Holds the certificates
@property (nonatomic, strong) NSMutableArray<NSData*> *certificates;


@end

@implementation CertificateHandler

- (instancetype)init {
    self = [super init];
    if (self) {
        _certificates = [NSMutableArray array];
    }
    return self;
}

// Collect the host certificates using the certificate check of the NSURLSessionTaskDelegate protocol
- (void)URLSession:(NSURLSession *)session
              task:(NSURLSessionTask *)task
didReceiveChallenge:(NSURLAuthenticationChallenge *)challenge
 completionHandler:(void (^)(NSURLSessionAuthChallengeDisposition, NSURLCredential * _Nullable))completionHandler {
    // Ignore any requests that are not related to server trust
    if (![challenge.protectionSpace.authenticationMethod isEqualToString:NSURLAuthenticationMethodServerTrust])
        return;
    // Check we have a server trust
    SecTrustRef serverTrust = challenge.protectionSpace.serverTrust;
    if (!serverTrust) {
        completionHandler(NSURLSessionAuthChallengeCancelAuthenticationChallenge, nil);
        return;
    }

    // Check the validity of the server trust
    if (@available(iOS 12.0, *)) {
        if (!SecTrustEvaluateWithError(serverTrust, nil)) {
            completionHandler(NSURLSessionAuthChallengeCancelAuthenticationChallenge, nil);
            return;
        }
    }
    else {
        SecTrustResultType result;
        OSStatus status = SecTrustEvaluate(serverTrust, &result);
        if (errSecSuccess != status) {
            completionHandler(NSURLSessionAuthChallengeCancelAuthenticationChallenge, nil);
            return;
        }
    }

    // Collect all the certs in the chain
    CFIndex certCount = SecTrustGetCertificateCount(serverTrust);
    for (int certIndex = 0; certIndex < certCount; certIndex++) {
        // Get the chain certificate - note that this function is deprecated from iOS 15 but the
        // replacement function is only available from iOS 15 and has a very different interface so
        // we can't use it yet
        SecCertificateRef cert = SecTrustGetCertificateAtIndex(serverTrust, certIndex);
        if (!cert) {
            completionHandler(NSURLSessionAuthChallengeCancelAuthenticationChallenge, nil);
            return;
        }
        NSData *certData = (NSData *) CFBridgingRelease(SecCertificateCopyData(cert));
        [_certificates addObject:certData];
    }//for
    // Fail the challenge as we only wanted the certificates
    completionHandler(NSURLSessionAuthChallengeCancelAuthenticationChallenge, nil);
}

- (void)storeCertificatesFromServerTrust:(SecTrustRef)serverTrust {
    CFIndex count = SecTrustGetCertificateCount(serverTrust);
    NSMutableArray *serverCertificates = [NSMutableArray array];
    for (CFIndex i = 0; i < count; i++) {
        SecCertificateRef certificate = SecTrustGetCertificateAtIndex(serverTrust, i);
        if (certificate) {
            [self.certificates addObject:(__bridge id)certificate];
        }
    }
}

@end

// Pinning cache implementation
// Declare the global mutable dictionary: This should not be accessed directly
// but rather from the functions defined bellow
NSMutableDictionary *globalCacheDictionary;


// Function to initialize the global dictionary
void initializeGlobalCacheDictionary() {
    globalCacheDictionary = [NSMutableDictionary dictionary];
}

// Function to add an object to the global dictionary
void addToGlobalCache(NSString* key,NSData* value) {
    @synchronized (globalCacheDictionary) {
        [globalCacheDictionary setObject:value forKey:key];
    }
}

// Function to retrieve an object from the global dictionary
id retrieveFromGlobalCacheDictionary(NSString *key) {
    @synchronized (globalCacheDictionary) {
        if (globalCacheDictionary == nil) {
            return nil;
        }
        return globalCacheDictionary[key];
    }
}

// Function to remove an object from the global dictionary
void removeFromGlobalCacheDictionary(NSString *key) {
    @synchronized (globalCacheDictionary) {
        [globalCacheDictionary removeObjectForKey:key];
    }
}

// This function should be accessible to C#
extern "C" {
    void Approov_emptyGlobalCacheDictionary();
}
// Function to empty the global dictionary
void Approov_emptyGlobalCacheDictionary() {
    @synchronized (globalCacheDictionary) {
        [globalCacheDictionary removeAllObjects];
    }
}


/* As defined in ApproovBridge.cs
*   [Serializable]
    public struct ApproovTokenFetchResult
    {
        public ApproovTokenFetchStatus status;
        public string ARC;
        public bool isForceApplyPins;
        public string token;
        public string rejectionReasons;
        public bool isConfigChanged;
        public string secureString;
        byte[] measurementConfig;
        string loggableToken;
    }
*
*/

// Convert the ApproovTokenFetchResult to a dictionary<NSString*,NSObject*>
// and then to a JSON string to be returned to C#
char* Approov_convertApproovTokenFetchResultToJSON(ApproovTokenFetchResult *result) {
    // Check if result.secureString is nil and insert an empty string if it is
    NSObject *secureStringObject = result.secureString ? result.secureString : [NSNull null];
    
    NSArray *measurementConfigArray = nil;
    if (result.measurementConfig) {

        NSUInteger len = [result.measurementConfig length];
        uint8_t *bytes = (uint8_t *)[result.measurementConfig bytes];
        NSMutableArray *byteArray = [NSMutableArray arrayWithCapacity:len];
        for (NSUInteger i = 0; i < len; i++) {
            [byteArray addObject:@(bytes[i])];
        }
        measurementConfigArray = [byteArray copy];
    }
    
    // Convert the ApproovTokenFetchResult to a dictionary<NSString*,NSObject*>
    NSMutableDictionary<NSString*,NSObject*> *dict = [NSMutableDictionary dictionary];
    [dict setObject:@(result.status) forKey:@"status"];
    [dict setObject:result.token forKey:@"token"];
    [dict setObject:secureStringObject forKey:@"secureString"];
    [dict setObject:result.ARC forKey:@"ARC"];
    [dict setObject:result.rejectionReasons forKey:@"rejectionReasons"];
    [dict setObject:@(result.isConfigChanged) forKey:@"isConfigChanged"];
    [dict setObject:@(result.isForceApplyPins) forKey:@"isForceApplyPins"];
    [dict setObject:result.loggableToken forKey:@"loggableToken"];
    
    if(measurementConfigArray) {
        [dict setObject:measurementConfigArray forKey:@"measurementConfig"];
    } else {
        [dict setObject:[NSNull null] forKey:@"measurementConfig"];
    }

    // Convert the dictionary to a JSON string
    NSError *error;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:dict options:0 error:&error];
    if (!jsonData) {
        NSLog(@"Error converting ApproovTokenFetchResult to JSON: %@", error);
        return nullptr;
    }

    // Convert the JSON data to a C-style string
    const char *cString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding].UTF8String;

    // Create a copy of the string to return
    char *aStringCopy = (char*)malloc(strlen(cString) + 1);
    if (aStringCopy == NULL) {
        return NULL;
    }
    memcpy(aStringCopy, cString, strlen(cString) + 1);
    return aStringCopy;

}


extern "C" {
    const char* Approov_stringFromApproovTokenFetchStatus(int approovTokenFetchStatus);
}

const char* Approov_stringFromApproovTokenFetchStatus(int approovTokenFetchStatus) {
        // Cast the integer status to the appropriate enum value
        ApproovTokenFetchStatus status = (ApproovTokenFetchStatus)approovTokenFetchStatus;

        // Call the stringFromApproovTokenFetchStatus method
        NSString *result = [Approov stringFromApproovTokenFetchStatus:status];

        // Convert the NSString to a C-style string
        const char *cString = [result UTF8String];

        // Create a copy of the string to return
        char *aStringCopy = (char*)malloc(strlen(cString) + 1);
        if (aStringCopy == NULL) {
            return NULL;
        }
        memcpy(aStringCopy, cString, strlen(cString) + 1);
        return aStringCopy;
}

extern "C" {
    bool Approov_initialize(const char *initialConfig, const char *updateConfig, const char *comment, NSError *__autoreleasing *error);
}

bool Approov_initialize(const char *initialConfig, const char *updateConfig, const char *comment, NSError *__autoreleasing *error) {
        // Call the initialize method
        BOOL success = [Approov initialize:[NSString stringWithUTF8String:initialConfig] updateConfig:updateConfig ? [NSString stringWithUTF8String:updateConfig] : nil comment:comment ? [NSString stringWithUTF8String:comment] : nil error:error];
        return success;
}

extern "C" {
    char* Approov_fetchApproovTokenAndWait(const char *url);
}

char* Approov_fetchApproovTokenAndWait(const char *url) {
    if (url == NULL) {
        return NULL;
    }
    // Call the fetchApproovTokenAndWait method
    ApproovTokenFetchResult* result = [Approov fetchApproovTokenAndWait: [NSString stringWithUTF8String:url]];
    char* aReturnValue = Approov_convertApproovTokenFetchResultToJSON(result);
    return aReturnValue;
}

extern "C" {
    char* Approov_fetchCustomJWTAndWait(const char *payload);
}

char* Approov_fetchCustomJWTAndWait(const char* payload) {
    if (payload == NULL) {
        return NULL;
    }
    // Call the fetchCustomJWTAndWait method
    ApproovTokenFetchResult* result = [Approov fetchCustomJWTAndWait:[NSString stringWithUTF8String:payload]];
    char* aReturnValue = Approov_convertApproovTokenFetchResultToJSON(result);
    return aReturnValue;
}

extern "C" {
    char* Approov_fetchSecureStringAndWait(const char* key, const char* newDef);
}

char* Approov_fetchSecureStringAndWait(const char* key, const char* newDef) {
    if (key == NULL) {
        return NULL;
    }
    // Call the fetchSecureStringAndWait method
    NSString* aNewDef;
    if (newDef == NULL) {
        aNewDef = nil;
    } else {
        aNewDef = [NSString stringWithUTF8String:newDef];
    }
    ApproovTokenFetchResult* result = [Approov fetchSecureStringAndWait:[NSString stringWithUTF8String:key] :aNewDef];
    char* aReturnValue = Approov_convertApproovTokenFetchResultToJSON(result);
    return aReturnValue;
}

extern "C" {
    char* Approov_fetchConfig();
}

char* Approov_fetchConfig() {
    // Call the fetchConfig method
    NSString *result = [Approov fetchConfig];
    char* resultString = (char*)[result cStringUsingEncoding:NSUTF8StringEncoding];
    // Create a copy of the string to return
    char* aStringCopy = (char*)malloc(strlen(resultString) + 1);
    if (aStringCopy == NULL) {
        return NULL;
    }
    memcpy(aStringCopy, resultString, strlen(resultString) + 1);
    return aStringCopy;
}

extern "C" {
    char* Approov_getPinsJSON(char* pinType);
}

char* Approov_getPinsJSON(char* pinType) {
    if (pinType == NULL) {
        return NULL;
    }
    // Call the getPinsJSON method
    NSString *result = [Approov getPinsJSON:[NSString stringWithUTF8String:pinType]];
    // Create a copy of the string to return
    char* aCstringResult = (char*)[result cStringUsingEncoding:NSUTF8StringEncoding];
    char* aStringCopy = (char*)malloc(strlen(aCstringResult) + 1);
    if (aStringCopy == NULL) {
        return NULL;
    }
    memcpy(aStringCopy, aCstringResult, strlen(aCstringResult) + 1);
    return aStringCopy;
}


extern "C" {
    void Approov_setUserProperty(const char *property);
}

void Approov_setUserProperty(const char *property) {
    if (property == NULL) {
        return;
    }
    // Call the setUserProperty method
    [Approov setUserProperty:[NSString stringWithUTF8String:property]];
}

extern "C" {
    void Approov_setDevKey(const char *key);
}

void Approov_setDevKey(const char *key) {
    if (key == NULL) {
        return;
    }
    // Call the setDevKey method
    [Approov setDevKey:[NSString stringWithUTF8String:key]];
}


extern "C" {
    void Approov_setDataHashInToken(char* data);
}

void Approov_setDataHashInToken(char* data) {
    if (data == NULL) {
        return;
    }
    // Call the setDataHashInToken method
    [Approov setDataHashInToken:[NSString stringWithUTF8String:data]];
}

// NOTE in objectiveC: typedef unsigned char Byte;
extern "C" {
    char* Approov_getIntegrityMeasurementProof(Byte* nonce, int nonceLength, Byte* measurementConfig, int measurementConfigLength);
}

char* Approov_getIntegrityMeasurementProof(Byte* nonce, int nonceLength, Byte* measurementConfig, int measurementConfigLength) {
    // Convert to NSData
    NSData *nonceData = [NSData dataWithBytes:nonce length:nonceLength];
    NSData *configData = [NSData dataWithBytes:measurementConfig length:measurementConfigLength];
    // Call the getIntegrityMeasurementProof method
    NSData *resultData = [Approov getIntegrityMeasurementProof:nonceData :configData];
    if (resultData == nil) {
        return NULL;
    }
    const char* resultCString = (const char*)[resultData bytes];
    // Create a copy of the string to return
    char* aStringCopy = (char*)malloc(resultData.length + 1);
    if (aStringCopy == NULL) {
        return NULL;
    }
    memcpy(aStringCopy, resultCString, strlen(resultCString) + 1);
    return aStringCopy;
}

extern "C" {
    char* Approov_getDeviceMeasurementProof(Byte* nonce, int nonceLength, Byte* measurementConfig, int measurementConfigLength);
}

char* Approov_getDeviceMeasurementProof(Byte* nonce, int nonceLength, Byte* measurementConfig, int measurementConfigLength) {
    // Convert to NSData
    NSData *nonceData = [NSData dataWithBytes:nonce length:nonceLength];
    NSData *configData = [NSData dataWithBytes:measurementConfig length:measurementConfigLength];
    // Call the getDeviceMeasurementProof method
    NSData *resultData = [Approov getDeviceMeasurementProof:nonceData :configData];
    if (resultData == nil) {
        return NULL;
    }
    const char* resultCString = (const char*)[resultData bytes];
    // Create a copy of the string to return
    char* aStringCopy = (char*)malloc(resultData.length + 1);
    if (aStringCopy == NULL) {
        return NULL;
    }
    memcpy(aStringCopy, resultCString, strlen(resultCString) + 1);
    return aStringCopy;
}


extern "C" {
    char* Approov_getDeviceID();
}

char* Approov_getDeviceID() {
    // Call the getDeviceID method
    NSString *result = [Approov getDeviceID];
    char* resultString = (char*)[result cStringUsingEncoding:NSUTF8StringEncoding];
    // Create a copy of the string to return
    char* aStringCopy = (char*)malloc(strlen(resultString) + 1);
    if (aStringCopy == NULL) {
        return NULL;
    }
    memcpy(aStringCopy, resultString, strlen(resultString) + 1);
    return aStringCopy;
}


extern "C" {
    char* Approov_getMessageSignature(Byte* message, int messageLength);
}

char* Approov_getMessageSignature(Byte* message, int messageLength) {
    if (message == NULL) {
        return NULL;
    }
    // Convert to NSString
    NSString *str = [[NSString alloc] initWithBytes:message length:messageLength encoding:NSUTF8StringEncoding];
    // Call the getMessageSignature method
    NSString *resultString = [Approov getMessageSignature:str];
    const char* resultCString = [resultString UTF8String];
    // Create a copy of the string to return
    char* aStringCopy = (char*)malloc(strlen(resultCString) + 1);
    if (aStringCopy == NULL) {
        return NULL;
    }
    memcpy(aStringCopy, resultCString, strlen(resultCString) + 1);
    return aStringCopy;
}

/* SPKI headers for each key type and size */
unsigned char rsa2048SPKIHeader[] = {
    0x30, 0x82, 0x01, 0x22, 0x30, 0x0d, 0x06, 0x09,
    0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01,
    0x01, 0x05, 0x00, 0x03, 0x82, 0x01, 0x0f, 0x00
    };
unsigned char rsa3072SPKIHeader[] = {
    0x30, 0x82, 0x01, 0xa2, 0x30, 0x0d, 0x06, 0x09,
    0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01,
    0x01, 0x05, 0x00, 0x03, 0x82, 0x01, 0x8f, 0x00
    };
unsigned char rsa4096SPKIHeader[] = {
    0x30, 0x82, 0x02, 0x22, 0x30, 0x0d, 0x06, 0x09,
    0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01,
    0x01, 0x05, 0x00, 0x03, 0x82, 0x02, 0x0f, 0x00
    };
unsigned char ecdsaSecp256r1SPKIHeader[] = {
    0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2a, 0x86,
    0x48, 0xce, 0x3d, 0x02, 0x01, 0x06, 0x08, 0x2a,
    0x86, 0x48, 0xce, 0x3d, 0x03, 0x01, 0x07, 0x03,
    0x42, 0x00
    };
unsigned char ecdsaSecp384r1SPKIHeader[] = {
    0x30, 0x76, 0x30, 0x10, 0x06, 0x07, 0x2a, 0x86,
    0x48, 0xce, 0x3d, 0x02, 0x01, 0x06, 0x05, 0x2b,
    0x81, 0x04, 0x00, 0x22, 0x03, 0x62, 0x00
    };

NSString* getPublicKeyWithHeader(NSData* certData) {
    // Extract the public key from the certificate data
    SecCertificateRef cert = SecCertificateCreateWithData(NULL, (__bridge CFDataRef)certData);
    SecTrustRef trust;
    SecTrustCreateWithCertificates(cert, SecPolicyCreateBasicX509(), &trust);
    SecKeyRef publicKey = SecCertificateCopyKey(cert);
    // The return value
    NSMutableData* publicKeyData = nil;
    if (publicKey) {
        // Convert public key to NSData
        CFDataRef keyDataRef = SecKeyCopyExternalRepresentation(publicKey, NULL);
        NSData* keyData = (__bridge NSData *)keyDataRef;
        if (!keyData) {
            NSLog(@"Failed to extract public key data");
            return nil;
        }
        // Get the key type and size
         CFDictionaryRef attributes = SecKeyCopyAttributes(publicKey);
    
        if (attributes) {
            CFTypeRef keyType = CFDictionaryGetValue(attributes, kSecAttrKeyType);
            CFNumberRef keySizeNumber = (CFNumberRef)CFDictionaryGetValue(attributes, kSecAttrKeySizeInBits);
            int keySize;
            if (!CFNumberGetValue(keySizeNumber, kCFNumberIntType, &keySize)) {
                NSLog(@"Failed to read public key length");
                return nil;
            }
            
            if (keyType == kSecAttrKeyTypeRSA) {
                NSLog(@"Key type: RSA");
                switch(keySize) {
                    case 2048:
                        NSLog(@"Key size: 2048");
                        publicKeyData = [NSMutableData dataWithBytes:rsa2048SPKIHeader length:sizeof(rsa2048SPKIHeader)];
                        [publicKeyData appendData:keyData];
                        break;
                    case 3072:
                        NSLog(@"Key size: 3072");
                        publicKeyData = [NSMutableData dataWithBytes:rsa3072SPKIHeader length:sizeof(rsa3072SPKIHeader)];
                        [publicKeyData appendData:keyData];
                        break;
                    case 4096:
                        NSLog(@"Key size: 4096");
                        publicKeyData = [NSMutableData dataWithBytes:rsa4096SPKIHeader length:sizeof(rsa4096SPKIHeader)];
                        [publicKeyData appendData:keyData];
                        break;
                    default:
                        NSLog(@"Key size: %d", keySize);    // This should force us to return nil
                        break;
                }
            } else if (keyType == kSecAttrKeyTypeECSECPrimeRandom) {
                NSLog(@"Key type: ECC");
                switch(keySize) {
                    case 256:
                        NSLog(@"Key size: 256");
                        publicKeyData = [NSMutableData dataWithBytes:ecdsaSecp256r1SPKIHeader length:sizeof(ecdsaSecp256r1SPKIHeader)];
                        [publicKeyData appendData:keyData];
                        break;
                    case 384:
                        NSLog(@"Key size: 384");
                        publicKeyData = [NSMutableData dataWithBytes:ecdsaSecp384r1SPKIHeader length:sizeof(ecdsaSecp384r1SPKIHeader)];
                        [publicKeyData appendData:keyData];
                        break;
                    default:
                        NSLog(@"Key size: %d", keySize);    // This should force us to return nil
                        break;
                
                }
            } else {
                NSLog(@"Unknown Key type: %@", keyType);    // This should force us to return nil
            }
            CFRelease(attributes);
        }
    CFRelease(keyDataRef);
    CFRelease(publicKey);
    } else {
        NSLog(@"%@ Error extracting public key from certificate!", TAG);
        return nil;
    }
    CFRelease(trust);
    CFRelease(cert);
    // We compute the SHA256 hash of the public key
    uint8_t digest[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(publicKeyData.bytes, (CC_LONG)publicKeyData.length, digest);
    // Return NSString of the above in base64 representation
    NSData* sha256Data = [NSData dataWithBytes:digest length:CC_SHA256_DIGEST_LENGTH];
    NSString* sha256String = [sha256Data base64EncodedStringWithOptions:0];
    return sha256String;
}


/*  Computes a base64 string based on the pinType variable. This could be a public key or certificate.
*   @param cert: the certificate to operate on
*   @param pinType: the type of pinning to obtain
*   @return the base64 string of the computed pin or null if the pinning type is not recognized
*/
NSString* getCertPinForPinType(NSData* certData, NSString* pinningType) {
    // Create a certificate from NSData
    SecCertificateRef certificate = SecCertificateCreateWithData(NULL, (__bridge CFDataRef)certData);
    if (certificate == NULL) {
        NSLog(@"Unable to create certificate");
        return nil;
    }

    if ([pinningType isEqualToString:@"public-key-sha256"]) {
        NSString* publicKey = getPublicKeyWithHeader(certData);
        // Clean up
        CFRelease(certificate);
        return publicKey;
    } else {
        // Not implemented
        NSLog(@"Pinning type not recognized: %@", pinningType);
        return nil;
    }

}

/*  Check if the Approov SDK contains the pin pKey for host pHost
*  @param pHost: the host to check
*  @param pKey: the public key to check
*  @param pKeyType: the type of the pin
*  @return true if the pin is in the Approov SDK, false otherwise
*/
BOOL checkPinForHostIsSetInApproov(NSString* pHost, NSString* pKey, NSString* pKeyType) {
    // Get the pins for the host defined in Approov: Note at this point the SDK is already initialized
    NSDictionary<NSString *, NSArray<NSString *> *> *approovPins = [Approov getPins:pKeyType];
    if (approovPins == nil) {
        NSLog(@"ApproovBridge: Unable to get pins from Approov SDK");
        return false;
    }
    // Check if hostname is in the keys of the dictionary
    if (![approovPins objectForKey:pHost]) {
        NSLog(@"Host %@ not found in Approov SDK pins", pHost);
        return NO;
    }

    // Check if the pin is in the list of pins for the host
    NSArray<NSString *> *pins = [approovPins objectForKey:pHost];
    if (pins == nil || pins.count == 0) { // We should not have a nil value but an empty one can happen
        NSLog(@"Pins for host %@ are null or empty", pHost);
        return NO;
    }
    
    // Iterate over the pins and check if the pin is in the list
    for (NSString *pin in pins) {
        if ([pin isEqualToString:pKey]) {
            NSLog(@"Pin %@ found in Approov SDK pins for host %@", pKey, pHost);
            return YES;
        }
    }
    
    // Default to quiet compiler
    return NO;
}

/*  Connect to hostname and fetch certificates
*   Returns a list of certificates in byte array format
*   @param hostname: the hostname to connect to
*   @return a list of certificates in byte array format
*/
NSArray<NSData *> *fetchCertificatesForHost(NSString* hostname) {
    // Check if the hostname already contains "https://"
    if (![hostname hasPrefix:@"https://"]) {
        hostname = [@"https://" stringByAppendingString:hostname];
    }
    
    // Create a URL from the hostname
    NSURL *url = [NSURL URLWithString:hostname];
    
    if (url == nil) {
        NSLog(@"Invalid URL: %@", hostname);
        return nil;
    }
    
    // Create the download certificates handler
    CertificateHandler *handler = [[CertificateHandler alloc] init];
    // Create the Session
    NSURLSessionConfiguration *sessionConfig = [NSURLSessionConfiguration ephemeralSessionConfiguration];
    sessionConfig.timeoutIntervalForResource = FETCH_CERTIFICATES_TIMEOUT;
    NSURLSession* URLSession = [NSURLSession sessionWithConfiguration:sessionConfig delegate:handler delegateQueue:nil];

    // Create the request
    NSMutableURLRequest *certFetchRequest = [NSMutableURLRequest requestWithURL:url];
    [certFetchRequest setTimeoutInterval:FETCH_CERTIFICATES_TIMEOUT];
    [certFetchRequest setHTTPMethod:@"GET"];

    // Set up a semaphore so we can detect when the request completed
    dispatch_semaphore_t certFetchComplete = dispatch_semaphore_create(0);

    // Get session task to issue the request, write back any error on completion and signal the semaphore
    // to indicate that it is complete
    __block NSError *certFetchError = nil;
    NSURLSessionTask *certFetchTask = [URLSession dataTaskWithRequest:certFetchRequest
        completionHandler:^(NSData *data, NSURLResponse *response, NSError *error)
        {
            certFetchError = error;
            dispatch_semaphore_signal(certFetchComplete);
        }];

    // Make the request
    [certFetchTask resume];

    // Wait on the semaphore which shows when the network request is completed - note we do not use
    // a timeout here since the NSURLSessionTask has its own timeouts
    dispatch_semaphore_wait(certFetchComplete, DISPATCH_TIME_FOREVER);

    // We expect error cancelled because URLSession:task:didReceiveChallenge:completionHandler: always deliberately
    // fails the challenge because we don't need the request to succeed to retrieve the certificates
    if (!certFetchError) {
        // If no error occurred, the certificate check of the NSURLSessionTaskDelegate protocol has not been called.
        //  Don't return any host certificates
        NSLog(@"Failed to get host certificates: Error: unknown\n");
        return nil;
    }
    if (certFetchError && (certFetchError.code != NSURLErrorCancelled)) {
        // If an error other than NSURLErrorCancelled occurred, don't return any host certificates
        NSLog(@"Failed to get host certificates: Error: %@\n", certFetchError.localizedDescription);
        return nil;
    }
    return handler.certificates;
}

/* Iterates over certificate chain and attempts to match pins to the ones in the Approov SDK
* @param hostCertificates: the certificate chain to validate
* @param hostname: the hostname of the server
* @param pinType: the type of pinning we are using to pin our connections
* @return null if the connection is pinned by approov or is not protected by Approov, an error message otherwise
*/
NSString* approovPinsValidation(NSArray<NSData*>* hostCertificates, NSString* hostname, NSString* pinType) {
    // 1. Get the approov pins for the host
    NSLog(@"pinType: %@", pinType);
    NSDictionary<NSString *, NSArray<NSString *> *> *approovPins = [Approov getPins:pinType];
    if (approovPins == nil) {
        NSString *errorMessage = [NSString stringWithFormat:@"%@Approov SDK pins are null", TAG];
        NSLog(@"%@", errorMessage);
        return errorMessage;
    }
    NSLog(@"approovPins: %@", approovPins);
    // The list of pins for current host
    NSArray<NSString *> *allPinsForHost = [approovPins objectForKey:hostname];
    NSLog(@"allPinsForHost: %@", allPinsForHost);
    // if there are no pins for the domain (but the host is present) then use any managed trust roots instead
    if ((allPinsForHost != nil) && (allPinsForHost.count == 0)) {
        // Get the wildcard pins for managed trust roots
        allPinsForHost = [approovPins objectForKey:@"*"];
    }
    // if we are not pinning then we consider this level of trust to be acceptable
    if ((allPinsForHost == nil) || (allPinsForHost.count == 0)) {
        NSLog(@"%d", allPinsForHost.count);
        // Host is not being pinned and we have succesfully checked certificate chain as valid
        NSLog(@"%@ Host not pinned %@", TAG, hostname);
        return nil;
    }
    
    
    // 2. Iterate over certificate chain, extract pinning information and compare with Approov pins
    for (NSData *certificate in hostCertificates) {
        NSString *evaluatedPin = getCertPinForPinType(certificate, pinType);
        if (evaluatedPin == nil) {
            NSString *errorMessage = [NSString stringWithFormat:@"%@ Unable to extract pinning information from certificate", TAG];
            NSLog(@"%@", errorMessage);
            return errorMessage;
        }
        if (checkPinForHostIsSetInApproov(hostname, evaluatedPin,pinType)) {
            NSLog(@"%@%@ Matched pin %@ from %lu pins for pin type: %@", TAG, hostname, evaluatedPin, (unsigned long)[allPinsForHost count], pinType);
            return nil;
        }
    }
    // No public key matched the pins and we have checked the whole chain
    NSString *errorMessage = [NSString stringWithFormat:@"%@%@ No matching pins from %lu pins for pin type %@", TAG, hostname, (unsigned long)[allPinsForHost count], pinType];
    NSLog(@"%@", errorMessage);
    return errorMessage;
}

extern "C" {
    /* Check if the connection should proceed with the given certificate
     * @param cert: the leaf certificate presented to us by the server
     * @param certLength: the length of the cert array
     * @param hostname: the hostname of the server
     * @param hostnameLength: the length of the hostname array
     * @param pinType: the type of the public key we are using to pin our connections
     * @param pinTypeLength: the length of the pinType array
     * @return SUCCESS string if connection should proceed or error message otherwise
    */
    char* Approov_shouldProceedWithConnection(Byte* cert, int certLength, char* hostname, int hostnameLength,
                                                    char* pinType, int pinTypeLength);
}

char* Approov_shouldProceedWithConnection(Byte* cert, int certLength, char* hostname, int hostnameLength,
                                                    char* pinType, int pinTypeLength) {
    if (hostname == NULL || pinType == NULL) {
        const char* errorMessage = "Hostname or pinning type can not be null";
        return (char*)errorMessage;
    }
    // Get the pinning information from the certificate
    NSString* hostnameString = [[NSString alloc] initWithBytes:hostname length:hostnameLength encoding:NSUTF8StringEncoding];
    NSString* pinningStringType = [[NSString alloc] initWithBytes:pinType length:pinTypeLength encoding:NSUTF8StringEncoding];
    NSData* certData = [NSData dataWithBytes:cert length:certLength];
    NSString* pinningString = getCertPinForPinType(certData, pinningStringType);

    if (pinningString == nil) {
        const char* errorMessage = "ApproovBridge: Unable to extract pinning information from certificate";
        return (char*)errorMessage;
    }
    // Check if the pinning string is in the set of pins present in Approov
    if (checkPinForHostIsSetInApproov(hostnameString, pinningString, pinningStringType)) {
        NSString* message = [@"ApproovBridge: Leaf cert pin, connection allowed for host " stringByAppendingString: hostnameString];
        NSLog(@"%@", message);
        return (char*)[SUCCESS UTF8String];
    }

    // The pinning info from leaf certificate is not in the Approov SDK, so we query the cache
    NSData* cachedLeafCertData = retrieveFromGlobalCacheDictionary(hostnameString);
    if(cachedLeafCertData) {
        // Compare the leaf cert to current one
        if (cachedLeafCertData == certData) {
            NSString* message = [@"ApproovBridge: Cached cert match, connection allowed for host " stringByAppendingString: hostnameString];
            NSLog(@"%@", message);
            return (char*)[SUCCESS UTF8String];
        } else {
            /* The leaf certificate is NOT present in cache: We DELETE the cached entry for host */
            NSLog(@"%@", [NSString stringWithUTF8String:"ApproovBridge: Leaf certificate hash found in cache, but does not match the one fetched from host"]);
            removeFromGlobalCacheDictionary(hostnameString);
        }
    }

    /* TLS lookup is needed now: we are either not pinning to leaf certificate,
    *   the leaf cert is not found in the cache, or the host is not pinned by Approov.
    *   We need to fetch the certificate chain and check the pins for the host
    */
    NSArray<NSData *> *certificates = fetchCertificatesForHost(hostnameString);
    if (certificates == nil) {
        NSString* message = [@"ApproovBridge: Failed to get certificates for host " stringByAppendingString: hostnameString];
        NSLog(@"%@", message);
        return (char*)[message UTF8String];
    } else if (certificates.count == 0) {
        NSString* message = [@"ApproovBridge: Certificate chain verification failed for host: " stringByAppendingString: hostnameString];
        NSLog(@"%@", message);
        return (char*)[message UTF8String];
    }
    // We need to have a leaf cert and at least one intermediate/root cert because we have already checked the leaf.
    if ([certificates count] < 2) {
        NSString* message = [@"ApproovBridge: Certificate chain too small for host " stringByAppendingString: hostnameString];
        NSLog(@"%@", message);
        return (char*)[message UTF8String];
    }
    // Check if the leaf certificate obtained matches the input certificate
    NSData* leafCertData = certificates[0];
    if (![leafCertData isEqualToData:certData]) {
        NSString* message = [@"ApproovBridge: Leaf certificate hash does not match the one fetched from host " stringByAppendingString: hostnameString];
        NSLog(@"%@", message);
        return (char*)[message UTF8String];
    }

    // Check pinning status
    NSString* resultMessage = approovPinsValidation(certificates, hostnameString, pinningStringType);
    if (resultMessage == nil) {
        NSLog(@"ApproovBridge: New cert chain verified and cached, connection allowed for host %@", hostnameString);
        // Add the chain to cache
        //void addToGlobalCache(NSString* key,NSString* value)
        addToGlobalCache(hostnameString, [certificates objectAtIndex:0]);
        return (char*)[SUCCESS UTF8String];
    }
    // We return the error message to C# land
    NSLog(@"ApproovBridge: error message during pinning: %@", resultMessage);
    return (char*)[resultMessage UTF8String];
}

