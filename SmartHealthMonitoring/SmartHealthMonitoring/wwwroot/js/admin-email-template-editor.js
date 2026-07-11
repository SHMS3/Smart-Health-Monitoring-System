(function () {
    const modeInput = document.getElementById('EditorMode');
    const htmlEditor = document.getElementById('HtmlContent');
    const bodyEditor = document.getElementById('BodyContent');
    const preview = document.getElementById('templatePreview');
    const refreshPreviewBtn = document.getElementById('refreshPreviewBtn');
    const visualPane = document.getElementById('visualPane');
    const advancedPane = document.getElementById('advancedPane');
    const initialPreviewHtml = document.getElementById('initialPreviewHtml');
    const form = document.getElementById('emailTemplateForm');

    if (!modeInput || !htmlEditor || !bodyEditor || !preview || !form) {
        return;
    }

    let currentMode = 'visual';

    if (window.tinymce) {
        window.tinymce.init({
            selector: '#BodyContent',
            height: 560,
            menubar: false,
            branding: false,
            plugins: 'lists link table code wordcount',
            toolbar: 'undo redo | blocks | bold italic underline forecolor | alignleft aligncenter alignright | bullist numlist | link table | removeformat',
            content_style: 'body { font-family: Arial, sans-serif; font-size: 15px; line-height: 1.65; color: #334155; }',
            setup: function (editor) {
                editor.on('change keyup', function () {
                    editor.save();
                });
            }
        });
    }

    function setMode(mode) {
        currentMode = mode;
        modeInput.value = mode;

        document.querySelectorAll('.mode-tab').forEach(function (button) {
            button.classList.toggle('active', button.dataset.mode === mode);
        });

        if (visualPane) {
            visualPane.classList.toggle('active', mode === 'visual');
        }

        if (advancedPane) {
            advancedPane.classList.toggle('active', mode === 'advanced');
        }
    }

    function getVisualBody() {
        const editor = window.tinymce ? window.tinymce.get('BodyContent') : null;
        return editor ? editor.getContent() : bodyEditor.value;
    }

    function replaceBody(fullHtml, bodyHtml) {
        const bodyRegex = new RegExp('<body([^>]*)>[\\s\\S]*?</' + 'body>', 'i');
        if (!bodyRegex.test(fullHtml)) {
            return bodyHtml;
        }

        return fullHtml.replace(bodyRegex, function (_, attrs) {
            return '<body' + attrs + '>' + bodyHtml + '</' + 'body>';
        });
    }

    document.querySelectorAll('.mode-tab').forEach(function (button) {
        button.addEventListener('click', function () {
            setMode(button.dataset.mode);
        });
    });

    if (refreshPreviewBtn) {
        refreshPreviewBtn.addEventListener('click', function () {
            if (currentMode === 'visual') {
                preview.srcdoc = replaceBody(htmlEditor.value, getVisualBody());
                return;
            }

            preview.srcdoc = htmlEditor.value;
        });
    }

    form.addEventListener('submit', function () {
        const editor = window.tinymce ? window.tinymce.get('BodyContent') : null;
        if (editor) {
            editor.save();
        }

        modeInput.value = currentMode;
    });

    document.querySelectorAll('.token-chip').forEach(function (button) {
        button.addEventListener('click', function () {
            const token = button.dataset.token;

            if (currentMode === 'visual') {
                const editor = window.tinymce ? window.tinymce.get('BodyContent') : null;
                if (editor) {
                    editor.insertContent(token);
                    editor.save();
                    return;
                }
            }

            const start = htmlEditor.selectionStart;
            const end = htmlEditor.selectionEnd;
            const before = htmlEditor.value.substring(0, start);
            const after = htmlEditor.value.substring(end);

            htmlEditor.value = before + token + after;
            htmlEditor.focus();
            htmlEditor.selectionStart = htmlEditor.selectionEnd = start + token.length;
        });
    });

    preview.srcdoc = initialPreviewHtml ? initialPreviewHtml.value : '';
    setMode('visual');
})();
