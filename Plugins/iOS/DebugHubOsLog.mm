#import <Foundation/Foundation.h>
#import <OSLog/OSLog.h>
#include <os/log.h>
#include <sys/sysctl.h>
#include <unistd.h>
#include "UnityInterface.h"

// Log của Unity (cả Debug.Log) và NSLog của SDK link tĩnh đều ra từ UnityFramework với subsystem rỗng: không
// tách được. Trampoline ghi log Unity qua UnitySetLogEntryHandler(LogToNSLogHandler) bằng OS_LOG_DEFAULT; hub thay
// handler đó bằng một bản y hệt nhưng ghi vào subsystem riêng, nên OsLog.cs nhận ra chính xác entry nào là của Unity.
static NSString* const UNITY_SUBSYSTEM = @"com.hlight.debughub.unity";

static os_log_t UnityLog()
{
    static os_log_t log;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ log = os_log_create(UNITY_SUBSYSTEM.UTF8String, "unity"); });
    return log;
}

// Như LogToNSLogHandler của Trampoline (UnityAppController.mm), chỉ khác subsystem.
static bool DebugHubLogEntry(LogType logType, const char* log, va_list list)
{
    NSString* format = log != NULL ? [NSString stringWithUTF8String:log] : nil;
    if (format == nil) return true;
    NSString* formatted = [[NSString alloc] initWithFormat:format arguments:list];
    os_log_with_type(UnityLog(), OS_LOG_TYPE_DEFAULT, "%{public}@", formatted);
    return true;
}

// Trampoline chỉ gắn handler os_log khi không có debugger (có debugger thì Unity ghi ra stdout): giữ đúng điều đó.
static bool DebuggerAttached()
{
    struct kinfo_proc info;
    info.kp_proc.p_flag = 0;
    int mib[4] = { CTL_KERN, KERN_PROC, KERN_PROC_PID, getpid() };
    size_t size = sizeof(info);
    if (sysctl(mib, 4, &info, &size, NULL, 0) != 0) return false;
    return (info.kp_proc.p_flag & P_TRACED) != 0;
}

// OsLog.cs gọi ở SubsystemRegistration — sau UnityInitTrampoline, nên ghi đè được handler của Trampoline.
extern "C" void DebugHub_MarkUnityLogs()
{
    if (!DebuggerAttached()) UnitySetLogEntryHandler(DebugHubLogEntry);
}

static char* Copy(NSString* text)
{
    const char* utf8 = text.UTF8String;
    return strdup(utf8 != NULL ? utf8 : "");
}

// OsLog.cs (spec ③): log của chính tiến trình mới hơn `after` (giây unix), mỗi entry
// `giây ␟ mức ␟ nguồn ␟ sender ␟ nội dung ␞` (nguồn U = log Unity, A = mọi thứ khác); `!lý do` khi không mở được
// store. strdup: IL2CPP free() chuỗi trả về.
extern "C" char* DebugHub_ReadLog(double after)
{
    if (@available(iOS 15.0, *))
    {
        @autoreleasepool
        {
            NSError* error = nil;
            OSLogStore* store = [OSLogStore storeWithScope:OSLogStoreCurrentProcessIdentifier error:&error];
            if (store == nil) return Copy([@"!" stringByAppendingString:error.localizedDescription ?: @"OSLogStore"]);

            OSLogPosition* position = [store positionWithDate:[NSDate dateWithTimeIntervalSince1970:after]];
            OSLogEnumerator* entries = [store entriesEnumeratorWithOptions:0 position:position predicate:nil error:&error];
            if (entries == nil) return Copy([@"!" stringByAppendingString:error.localizedDescription ?: @"OSLogEnumerator"]);

            NSMutableString* batch = [NSMutableString string];
            for (OSLogEntry* entry in entries)
            {
                if (![entry isKindOfClass:[OSLogEntryLog class]]) continue;
                OSLogEntryLog* log = (OSLogEntryLog*)entry;
                NSTimeInterval seconds = log.date.timeIntervalSince1970;
                if (seconds <= after) continue;

                char level = 'I';
                switch (log.level)
                {
                    case OSLogEntryLogLevelDebug: level = 'D'; break;
                    case OSLogEntryLogLevelNotice: level = 'N'; break;
                    case OSLogEntryLogLevelError: level = 'E'; break;
                    case OSLogEntryLogLevelFault: level = 'F'; break;
                    default: break;
                }
                char origin = [log.subsystem isEqualToString:UNITY_SUBSYSTEM] ? 'U' : 'A';
                [batch appendFormat:@"%.6f\x1f%c\x1f%c\x1f%@\x1f%@\x1e", seconds, level, origin, log.sender ?: @"", log.composedMessage ?: @""];
            }
            return Copy(batch);
        }
    }
    return strdup("");
}
