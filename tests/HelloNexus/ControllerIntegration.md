# Controller Integration Notes

- Adapter API inspected: Silk.NET Input 2.22.0.
- Recognized gamepads expose named `ButtonName` values, indexed `Thumbstick` values, and indexed `Trigger` values. Generic joysticks expose indexed buttons/axes and `Position2D` hats.
- Silk.NET 2.22.0's GLFW gamepad wrapper maps triggers from GLFW axes 4 and 5, preserving the raw `-1` released to `+1` engaged range with its default deadzone. Nexus normalizes those axes as unipolar signed values.
- Physical controller hardware was not exercised for this implementation. The actual runtime backend, reported control profile, button transitions, and analog neutral values remain unverified.
- `HelloNexus` logs detected controls and subsequent transitions/analog changes. Any controller's first logical button closes the demo window; connect a device, press/release a button, and move an analog control to complete the hardware check.