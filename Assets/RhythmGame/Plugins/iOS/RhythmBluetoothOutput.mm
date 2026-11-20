#import <AVFoundation/AVFoundation.h>

// Matches AudioOutputKind: Unknown=0, Other=1, Bluetooth=2.
extern "C" int RhythmBluetoothOutput()
{
    @autoreleasepool {
        NSArray<AVAudioSessionPortDescription *> *outputs = [AVAudioSession sharedInstance].currentRoute.outputs;
        if (outputs.count == 0) return 0;
        for (AVAudioSessionPortDescription *port in outputs) {
            if ([port.portType isEqualToString:AVAudioSessionPortBluetoothA2DP] ||
                [port.portType isEqualToString:AVAudioSessionPortBluetoothHFP] ||
                [port.portType isEqualToString:AVAudioSessionPortBluetoothLE]) return 2;
        }
        return 1;
    }
}
