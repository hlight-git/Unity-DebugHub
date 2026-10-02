#import <Foundation/Foundation.h>

// InstallSource.cs: TestFlight, ad-hoc và build từ Xcode có biên lai tên sandboxReceipt; bản App Store là receipt.
extern "C" int DebugHub_IsSandboxReceipt()
{
    NSURL *receipt = [[NSBundle mainBundle] appStoreReceiptURL];
    return receipt != nil && [[receipt lastPathComponent] isEqualToString:@"sandboxReceipt"] ? 1 : 0;
}
