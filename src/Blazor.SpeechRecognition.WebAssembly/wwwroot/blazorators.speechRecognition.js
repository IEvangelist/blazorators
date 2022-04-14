let _recognition = null;

/**
 * Cancels any active speech recognition session, 
 * considered best practice to properly clean up.
 * @param {boolean} isAborted
 */
export const cancelSpeechRecognition = (isAborted) => {
    if (_recognition !== null) {
        if (isAborted) {
            _recognition.abort();
        } else {
            _recognition.stop();
        }
        _recognition = null;
    }
};

/**
 * Starts recognizing speech in the browser, and registers 
 * all the callbacks for the given dotnetObj in context.
 * @param {any} dotnetObj
 * @param {string} lang The BCP47 tag for the language.
 * @param {string} key Used for round-trip verification and callback-receipts.
 * @param {string} onResultMethodName The callback to call for incremental recognition results.
 * @param {string | null} onErrorMethodName The optional callback to call in the event of an recognition error.
 * @param {string | null} onStartMethodName The optional callback to call when recognition started.
 * @param {string | null} onEndMethodName The optional callback to call when recognition ended.
 */
export const recognizeSpeech =
    (dotnetObj, lang, key, onResultMethodName,
        onErrorMethodName, onStartMethodName, onEndMethodName) => {
        if (!dotnetObj || !onResultMethodName) {
            return;
        }

        cancelSpeechRecognition(true);
        debugger;

        let recognitionImplementation = null;
        if (typeof (SpeechRecognition) !== "undefined" && recognitionImplementation == null)
            recognitionImplementation = SpeechRecognition;
        if (typeof (webkitSpeechRecognition) !== "undefined" && recognitionImplementation == null)
            recognitionImplementation = webkitSpeechRecognition;

        if (recognitionImplementation == null) {
            dotnetObj.invokeMethodAsync(onErrorMethodName, key, "This browser does not support any known SpeechRecognition method.");
            return;
        }

        _recognition = new recognitionImplementation();
        _recognition.continuous = true;
        _recognition.interimResults = true;
        _recognition.lang = lang;
        if (onStartMethodName) {
            _recognition.onstart = () => {
                dotnetObj.invokeMethod(onStartMethodName, key);
            };
        }
        if (onEndMethodName) {
            _recognition.onend = () => {
                dotnetObj.invokeMethod(onEndMethodName, key);
            };
        }
        if (onErrorMethodName) {
            _recognition.onerror = (error) => {
                dotnetObj.invokeMethod(onErrorMethodName, key, error);
            };
        }
        _recognition.onresult = (result) => {
            let transcript = '';
            let isFinal = false;
            for (let i = result.resultIndex; i < result.results.length; ++i) {
                transcript += result.results[i][0].transcript;
                if (result.results[i].isFinal) {
                    isFinal = true;
                }
            }
            if (isFinal) {
                const punctuation =
                    transcript.endsWith('.') ||
                    transcript.endsWith('?') ||
                    transcript.endsWith('!')
                        ? '' : '.';
                transcript =
                    `${transcript.replace(/\S/, str => str.toLocaleUpperCase())}${punctuation}`;
            }
            dotnetObj.invokeMethod(onResultMethodName, key, transcript, isFinal);
        };
        _recognition.start();
    };

window.addEventListener('beforeunload', _ => {
    cancelSpeechRecognition(true);
});