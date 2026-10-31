# Native audio authoring and observation

Use the current catalog contracts for `audio/create-mixer`,
`audio-mixer/transaction`, `audio-mixer/info`, and `audio/record-output`.
Creation uses Unity's native default mixer factory and rejects existing files.
Mixer transactions own groups, snapshots, effects and exposed parameters.
For an Attenuation effect, `Volume` selects its parent group's native volume;
`MixLevel` selects the distinct effect wet-mix parameter. Exposure constructs
Unity's concrete group/effect path and preserves the native parameter GUID.

Recording binds the exact existing active AudioListener component in stable
Play Mode. Begin returns a capability sessionId. Inspect reads its current
result; End publishes actual samples received early, while Cancel publishes no
WAV. A new absolute output path outside Assets and an existing parent directory
are required. The passive runtime-assembly filter does not alter samples or player preferences.
The capture stage is Unity's listener filter, before any downstream device,
driver, speaker or room processing. Inspect the source/mixer/listener settings
separately; do not call a waveform an acoustic measurement.

Successful recording requires nonempty native samples and a newly written
32-bit IEEE-float WAV with exact rate and channel metadata. Silence is a valid
observation with peak/RMS zero. Missing DSP callbacks, a changed channel layout,
nonfinite samples or a destroyed listener produce an explicit failure.
Play exit, domain reload and Editor shutdown cancel active observation and
release the filter. A session identity does not survive Editor reload.

## Static cost ledger

One active recording owns <=4,000,000 float samples (16MB). Duration <=15s;
sample count is ceil(duration*actual output rate)*actual configured channels.
Admission rejects larger products. Every DSP callback copies at most the
remaining admitted samples and allocates nothing. Terminal statistics are one
linear pass; WAV publication copies the same buffer in16KiB blocks, <=977
blocks/16MB output and32MiB total managed recording storage including transient
Unity callback data. No scene or Asset census, Cartesian product, polling copy
or runtime synthesis occurs. The listener callback and retirement exchange
ownership under one lock; Unity object removal runs only on the Editor thread.
Budget: <=16MB persistent PCM, <=16KiB writer scratch, one passive filter and
one Editor update subscription, all retired before resourcesReleased=true.
