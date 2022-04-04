// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

const speak = (argument) => {
    if (argument) {
        const utterance = new SpeechSynthesisUtterance();

        // Manually map values from the argument, as we cannot
        // instantiate objects from JSON.
        Object.entries(argument).forEach(entry => {
            const [key, value] = entry;
            if (value) {
                if (key === "voice") {
                    const voices = window.speechSynthesis.getVoices();
                    const voice = voices.find(v => v.name === value.name);
                    if (voice) {
                        utterance.voice = voice;
                    }
                } else {
                    utterance[key] = value;
                }
            }
        });

        utterance.onend = e => {
            DotNet.invokeMethod(
                "Blazor.SpeechSynthesis.WebAssembly",
                "OnUtteranceEnded",
                utterance.text,
                e.elapsedTime);
        };

        window.speechSynthesis.speak(utterance);
    }
}

const getVoices = () => {
    const mapVoices = (voices) => {
        return voices.map(voice => {
            // HACK: Blazor isn't correctly deserializing these.
            // It ignores the `JsonPropertyNameAttribute` :(
            return {
                Default: voice.default,
                Lang: voice.lang,
                LocalService: voice.localService,
                Name: voice.name,
                VoiceURI: voice.voiceURI
            };
        });
    };

    return new Promise((resolve, reject) => {
        let voices = window.speechSynthesis.getVoices();
        if (voices.length !== 0) {
            resolve(mapVoices(voices));
        } else {
            window.speechSynthesis.onvoiceschanged = () => {
                voices = window.speechSynthesis.getVoices();
                resolve(mapVoices(voices));
            };
        }
    });
};

const onVoicesChanged = (assemblyName, callbackMethodName, key) => {
    speechSynthesis.onvoiceschanged = () => {
        DotNet.invokeMethodAsync(assemblyName, callbackMethodName, key);
    };
};

const unsubscribeVoicesChanged = () => {
    speechSynthesis.onvoiceschanged = null;
};

window.blazorators = Object.assign({}, window.blazorators, {
    speechSynthesis: {
        speak,
        getVoices,
        onVoicesChanged,
        unsubscribeVoicesChanged
    }
});