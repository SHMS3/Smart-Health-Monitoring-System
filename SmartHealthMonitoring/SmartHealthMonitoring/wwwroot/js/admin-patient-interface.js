(() => {
    const form = document.getElementById('patientUiEditor');
    if (!form) return;

    const tabs = [...document.querySelectorAll('[data-panel-target]')];
    const panels = [...document.querySelectorAll('[data-editor-panel]')];
    const saveState = document.getElementById('patientUiSaveState');
    const saveStateText = document.getElementById('patientUiSaveStateText');
    const preview = document.getElementById('patientUiPreview');
    let isDirty = false;

    const activatePanel = (name, focusTab = false) => {
        tabs.forEach(tab => {
            const active = tab.dataset.panelTarget === name;
            tab.setAttribute('aria-selected', active.toString());
            tab.tabIndex = active ? 0 : -1;
            if (active && focusTab) tab.focus();
        });
        panels.forEach(panel => panel.hidden = panel.dataset.editorPanel !== name);
    };

    tabs.forEach((tab, index) => {
        tab.addEventListener('click', () => activatePanel(tab.dataset.panelTarget));
        tab.addEventListener('keydown', event => {
            if (!['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
            event.preventDefault();
            const step = event.key === 'ArrowRight' ? 1 : -1;
            const next = (index + step + tabs.length) % tabs.length;
            activatePanel(tabs[next].dataset.panelTarget, true);
        });
    });

    const markDirty = () => {
        if (isDirty) return;
        isDirty = true;
        saveState?.classList.add('is-dirty');
        if (saveStateText) saveStateText.textContent = 'Có thay đổi chưa lưu';
    };

    const updateTextPreview = input => {
        const key = input.dataset.previewField;
        if (!key) return;
        document.querySelectorAll(`[data-preview-value="${key}"]`)
            .forEach(target => target.textContent = input.value.trim() || '—');
    };

    form.querySelectorAll('[data-preview-field]').forEach(input => {
        input.addEventListener('input', () => updateTextPreview(input));
    });

    const attachCharacterCounter = input => {
        if (input.dataset.counterReady === 'true') return;
        input.dataset.counterReady = 'true';
        const counter = document.createElement('span');
        counter.className = 'pi-character-count';
        input.insertAdjacentElement('afterend', counter);
        const update = () => {
            const length = input.value.length;
            const max = Number(input.maxLength);
            counter.textContent = `${length}/${max} ký tự`;
            counter.classList.toggle('is-near-limit', length >= max * .85);
        };
        input.addEventListener('input', update);
        update();
    };

    form.querySelectorAll('[maxlength]').forEach(attachCharacterCounter);

    const getRowField = (row, fieldName) =>
        [...row.querySelectorAll('[name]')]
            .find(control => control.name.endsWith(`.${fieldName}`) && control.type !== 'hidden');

    const renderRepeaterPreview = container => {
        const prefix = container.dataset.repeater;
        const target = document.querySelector(`[data-repeater-preview="${prefix}"]`);
        if (!target) return;

        target.replaceChildren();
        const rows = [...container.querySelectorAll('[data-repeat-row]')];
        rows.forEach(row => {
            const label = getRowField(row, 'Label')?.value.trim() || '';
            const value = getRowField(row, 'Value')?.value.trim() || '';
            if (!label && !value) return;

            const previewRow = document.createElement('div');
            previewRow.className = 'pi-preview-list-row';
            const highlight = getRowField(row, 'Highlight');
            previewRow.classList.toggle('is-highlighted', Boolean(highlight?.checked));

            const labelElement = document.createElement('span');
            labelElement.textContent = label || 'Thông tin';
            const valueElement = document.createElement('strong');
            valueElement.textContent = value || '—';
            previewRow.append(labelElement, valueElement);
            target.append(previewRow);
        });

        if (!target.children.length) {
            const empty = document.createElement('span');
            empty.className = 'pi-preview-list-empty';
            empty.textContent = 'Chưa có dữ liệu';
            target.append(empty);
        }
    };

    const reindexRepeater = container => {
        const prefix = container.dataset.repeater;
        const rows = [...container.querySelectorAll('[data-repeat-row]')];

        rows.forEach((row, index) => {
            row.querySelectorAll('[name]').forEach(control => {
                const fieldName = control.name.split('.').pop();
                if (!fieldName) return;
                control.name = `${prefix}[${index}].${fieldName}`;
                if (control.id) control.id = `${prefix}_${index}__${fieldName}`;
            });

            row.querySelectorAll('.pi-field').forEach(field => {
                const control = field.querySelector('input:not([type="hidden"]), select, textarea');
                const label = field.querySelector('label');
                if (control?.id && label) label.htmlFor = control.id;
            });

            row.querySelectorAll('[data-valmsg-for]').forEach(message => {
                const fieldName = message.dataset.valmsgFor.split('.').pop();
                message.dataset.valmsgFor = `${prefix}[${index}].${fieldName}`;
            });
        });

        const maxItems = Number(container.dataset.maxItems || 12);
        const addButton = container.querySelector('[data-add-row]');
        const emptyState = container.querySelector('[data-repeat-empty]');
        if (addButton) {
            addButton.disabled = rows.length >= maxItems;
            addButton.title = rows.length >= maxItems ? `Tối đa ${maxItems} dòng` : '';
        }
        emptyState?.classList.toggle('d-none', rows.length > 0);
        renderRepeaterPreview(container);
    };

    document.querySelectorAll('[data-repeater]').forEach(container => {
        const list = container.querySelector('[data-repeat-list]');
        const addButton = container.querySelector('[data-add-row]');
        const template = document.getElementById(container.dataset.template);
        const maxItems = Number(container.dataset.maxItems || 12);
        if (!list || !template) return;

        addButton?.addEventListener('click', () => {
            const rowCount = list.querySelectorAll('[data-repeat-row]').length;
            if (rowCount >= maxItems) return;

            list.insertAdjacentHTML('beforeend', template.innerHTML.replaceAll('__index__', rowCount));
            const row = list.lastElementChild;
            row?.querySelectorAll('[maxlength]').forEach(attachCharacterCounter);
            reindexRepeater(container);

            if (window.jQuery?.validator?.unobtrusive && row) {
                window.jQuery.validator.unobtrusive.parse(row);
            }

            markDirty();
            row?.querySelector('input:not([type="hidden"]), select, textarea')?.focus();
        });

        container.addEventListener('click', event => {
            const removeButton = event.target.closest('[data-remove-row]');
            if (!removeButton) return;
            removeButton.closest('[data-repeat-row]')?.remove();
            reindexRepeater(container);
            markDirty();
        });

        container.addEventListener('input', () => renderRepeaterPreview(container));
        container.addEventListener('change', () => renderRepeaterPreview(container));
        reindexRepeater(container);
    });

    form.querySelectorAll('[data-preview-color]').forEach(input => {
        const output = document.querySelector(`[data-color-output="${input.id}"]`);
        const update = () => {
            preview?.style.setProperty(input.dataset.previewColor, input.value);
            if (output) output.textContent = input.value.toUpperCase();
        };
        input.addEventListener('input', update);
        update();
    });

    form.querySelectorAll('[data-preview-toggle]').forEach(input => {
        const update = () => {
            document.querySelectorAll(`[data-toggle-visibility="${input.dataset.previewToggle}"]`)
                .forEach(target => target.hidden = !input.checked);
        };
        input.addEventListener('change', update);
        update();
    });

    const logoSelect = document.getElementById('LogoIcon');
    const updateLogo = () => {
        document.querySelectorAll('[data-preview-logo]')
            .forEach(icon => icon.className = logoSelect.value);
    };
    logoSelect?.addEventListener('change', updateLogo);
    if (logoSelect) updateLogo();

    const bindImagePreview = (inputId, imageId, heroBackground = false) => {
        const input = document.getElementById(inputId);
        const image = document.getElementById(imageId);
        if (!input || !image) return;
        input.addEventListener('change', () => {
            const file = input.files?.[0];
            if (!file) return;
            const url = URL.createObjectURL(file);
            const images = [image, ...document.querySelectorAll(`[data-image-preview-for="${inputId}"]`)];
            images.forEach(target => target.src = url);
            image.onload = () => URL.revokeObjectURL(url);
            if (heroBackground && preview) {
                preview.style.setProperty('--preview-hero-image', `url("${url}")`);
            }
        });
    };

    bindImagePreview('HomeHeroImageFile', 'heroImagePreview', true);
    bindImagePreview('HomeAboutImageFile', 'aboutImagePreview');

    form.addEventListener('input', markDirty);
    form.addEventListener('change', markDirty);
    form.addEventListener('submit', () => {
        document.querySelectorAll('[data-repeater]').forEach(reindexRepeater);
        isDirty = false;
    });
    document.getElementById('patientUiResetForm')
        ?.addEventListener('submit', () => { isDirty = false; });
    form.addEventListener('invalid', event => {
        const panel = event.target.closest('[data-editor-panel]');
        if (panel) activatePanel(panel.dataset.editorPanel);
    }, true);

    window.addEventListener('beforeunload', event => {
        if (!isDirty) return;
        event.preventDefault();
        event.returnValue = '';
    });

    const firstError = document.querySelector('.field-validation-error:not(:empty)');
    const errorPanel = firstError?.closest('[data-editor-panel]');
    if (errorPanel) activatePanel(errorPanel.dataset.editorPanel);
})();
