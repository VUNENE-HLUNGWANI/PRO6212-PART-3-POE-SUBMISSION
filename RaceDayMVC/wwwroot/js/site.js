/* RaceDay shared behaviour (Orbit edition):
   starfield, aurora, cursor glow, transitions, nav, forms, paging,
   command palette, toasts, countdowns and motion. */
(function () {
    'use strict';

    document.documentElement.classList.add('js');

    var prefersReducedMotion = window.matchMedia
        && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var canHover = window.matchMedia && window.matchMedia('(pointer: fine)').matches;

    function toArray(list) { return [].slice.call(list); }

    /* ---------- Aurora backdrop injection ---------- */

    function initAurora() {
        if (document.querySelector('.rd-aurora')) { return; }
        var aurora = document.createElement('div');
        aurora.className = 'rd-aurora';
        aurora.setAttribute('aria-hidden', 'true');
        aurora.innerHTML = '<span></span>';
        document.body.insertBefore(aurora, document.body.firstChild);
    }

    /* ---------- Starfield ---------- */

    function initStarfield() {
        if (prefersReducedMotion) { return; }
        var canvas = document.createElement('canvas');
        canvas.className = 'rd-stars';
        canvas.setAttribute('aria-hidden', 'true');
        document.body.insertBefore(canvas, document.body.firstChild);

        var ctx = canvas.getContext('2d');
        var width = 0, height = 0, ratio = 1;
        var stars = [], shooting = [], constellations = [];
        var mouseX = 0, mouseY = 0;

        function resize() {
            ratio = Math.min(window.devicePixelRatio || 1, 2);
            width = window.innerWidth;
            height = window.innerHeight;
            canvas.width = width * ratio;
            canvas.height = height * ratio;
            canvas.style.width = width + 'px';
            canvas.style.height = height + 'px';
            ctx.setTransform(ratio, 0, 0, ratio, 0, 0);

            var count = Math.min(460, Math.floor((width * height) / 4800));
            stars = [];
            for (var i = 0; i < count; i++) {
                stars.push({
                    x: Math.random() * width,
                    y: Math.random() * height,
                    z: 0.2 + Math.random() * 0.8,
                    r: 0.3 + Math.random() * 1.2,
                    t: Math.random() * Math.PI * 2,
                    s: 0.01 + Math.random() * 0.03
                });
            }

            constellations = [];
            for (var c = 0; c < 5; c++) {
                var cx = Math.random() * width;
                var cy = Math.random() * height;
                var points = [];
                for (var p = 0; p < 4; p++) {
                    points.push({
                        x: cx + (Math.random() - 0.5) * 220,
                        y: cy + (Math.random() - 0.5) * 160
                    });
                }
                constellations.push(points);
            }
        }

        function spawnShootingStar() {
            shooting.push({
                x: Math.random() * width * 0.9,
                y: Math.random() * height * 0.45,
                vx: 7 + Math.random() * 6,
                vy: 3 + Math.random() * 3,
                life: 1
            });
        }

        function frame() {
            ctx.clearRect(0, 0, width, height);

            var offsetX = (mouseX - width / 2) * 0.02;
            var offsetY = (mouseY - height / 2) * 0.02;

            /* Constellations */
            ctx.strokeStyle = 'rgba(150, 170, 255, 0.07)';
            ctx.lineWidth = 1;
            for (var k = 0; k < constellations.length; k++) {
                var pts = constellations[k];
                ctx.beginPath();
                ctx.moveTo(pts[0].x + offsetX * 0.3, pts[0].y + offsetY * 0.3);
                for (var q = 1; q < pts.length; q++) {
                    ctx.lineTo(pts[q].x + offsetX * 0.3, pts[q].y + offsetY * 0.3);
                }
                ctx.stroke();
            }

            /* Stars */
            for (var i = 0; i < stars.length; i++) {
                var star = stars[i];
                star.t += star.s;
                var alpha = 0.35 + Math.sin(star.t) * 0.35;
                var x = star.x + offsetX * star.z;
                var y = star.y + offsetY * star.z;
                ctx.fillStyle = 'rgba(230, 236, 255, ' + alpha.toFixed(3) + ')';
                ctx.beginPath();
                ctx.arc(x, y, star.r * star.z + 0.2, 0, Math.PI * 2);
                ctx.fill();
            }

            if (Math.random() < 0.005) { spawnShootingStar(); }

            shooting = shooting.filter(function (s) { return s.life > 0; });
            for (var j = 0; j < shooting.length; j++) {
                var s = shooting[j];
                var tailX = s.x - s.vx * 14;
                var tailY = s.y - s.vy * 14;
                var gradient = ctx.createLinearGradient(s.x, s.y, tailX, tailY);
                gradient.addColorStop(0, 'rgba(255, 255, 255, 0.95)');
                gradient.addColorStop(1, 'rgba(255, 255, 255, 0)');
                ctx.strokeStyle = gradient;
                ctx.lineWidth = 1.6;
                ctx.beginPath();
                ctx.moveTo(s.x, s.y);
                ctx.lineTo(tailX, tailY);
                ctx.stroke();
                s.x += s.vx; s.y += s.vy; s.life -= 0.02;
            }

            window.requestAnimationFrame(frame);
        }

        window.addEventListener('resize', resize);
        window.addEventListener('pointermove', function (event) {
            mouseX = event.clientX; mouseY = event.clientY;
        }, { passive: true });

        resize();
        window.requestAnimationFrame(frame);
    }

    /* ---------- Cursor glow ---------- */

    function initCursorGlow() {
        if (prefersReducedMotion || !canHover) { return; }
        var glow = document.createElement('div');
        glow.className = 'rd-cursor';
        glow.setAttribute('aria-hidden', 'true');
        document.body.appendChild(glow);
        window.addEventListener('pointermove', function (event) {
            glow.style.setProperty('--cx', event.clientX + 'px');
            glow.style.setProperty('--cy', event.clientY + 'px');
            glow.classList.add('is-on');
        }, { passive: true });
    }

    /* ---------- Progress bar ---------- */

    function initProgress() {
        var bar = document.createElement('div');
        bar.className = 'rd-progress';
        bar.setAttribute('aria-hidden', 'true');
        document.body.appendChild(bar);
        function update() {
            var root = document.documentElement;
            var max = root.scrollHeight - root.clientHeight;
            var ratio = max > 0 ? root.scrollTop / max : 0;
            bar.style.transform = 'scaleX(' + ratio + ')';
        }
        window.addEventListener('scroll', update, { passive: true });
        update();
    }

    /* ---------- Page transitions ---------- */

    function initPageTransitions() {
        if (prefersReducedMotion) { return; }
        document.addEventListener('click', function (event) {
            if (event.defaultPrevented || event.button !== 0
                || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) { return; }
            var link = event.target.closest('a[href]');
            if (!link || link.target || link.hasAttribute('download')) { return; }
            var url = new URL(link.href, window.location.href);
            if (url.origin !== window.location.origin) { return; }
            if (url.pathname === window.location.pathname && url.search === window.location.search) { return; }
            event.preventDefault();
            document.body.classList.add('is-leaving');
            window.setTimeout(function () { window.location.href = url.href; }, 260);
        });
        window.addEventListener('pageshow', function () {
            document.body.classList.remove('is-leaving');
        });
    }

    /* ---------- Tilt + glow follow ---------- */

    function initTilt() {
        if (prefersReducedMotion || !canHover) { return; }
        var selector = '.rd-tile, .rd-programme, .rd-event-card, .rd-stat, .rd-policy, .rd-category, .rd-why-card, .rd-card, .rd-disc';
        toArray(document.querySelectorAll(selector)).forEach(function (card) {
            card.addEventListener('pointermove', function (event) {
                var rect = card.getBoundingClientRect();
                var x = (event.clientX - rect.left) / rect.width;
                var y = (event.clientY - rect.top) / rect.height;
                card.style.setProperty('--gx', (x * 100) + '%');
                card.style.setProperty('--gy', (y * 100) + '%');
                card.style.transform = 'perspective(900px) rotateX(' + ((0.5 - y) * 5) + 'deg) rotateY(' + ((x - 0.5) * 7) + 'deg) translateY(-3px)';
            });
            card.addEventListener('pointerleave', function () { card.style.transform = ''; });
        });
    }

    function initMagnetic() {
        if (prefersReducedMotion || !canHover) { return; }
        toArray(document.querySelectorAll('.rd-btn-lg')).forEach(function (button) {
            button.addEventListener('pointermove', function (event) {
                var rect = button.getBoundingClientRect();
                var x = event.clientX - (rect.left + rect.width / 2);
                var y = event.clientY - (rect.top + rect.height / 2);
                button.style.transform = 'translate(' + (x * 0.15) + 'px, ' + (y * 0.22) + 'px)';
            });
            button.addEventListener('pointerleave', function () { button.style.transform = ''; });
        });
    }

    /* ---------- Back buttons ---------- */

    document.addEventListener('click', function (event) {
        var button = event.target.closest('[data-back]');
        if (!button) { return; }
        event.preventDefault();
        var fallback = button.getAttribute('data-fallback') || '/';
        var sameSite = document.referrer.indexOf(window.location.host) !== -1;
        if (window.history.length > 1 && sameSite) { window.history.back(); }
        else { window.location.href = fallback; }
    });

    /* ---------- Form confirm: styled dialog for forms with data-confirm ---------- */

    var confirmDialog = null;
    var pendingForm = null;

    function getConfirmDialog() {
        if (confirmDialog) {
            return confirmDialog;
        }

        confirmDialog = document.createElement('dialog');
        confirmDialog.className = 'rd-confirm';
        confirmDialog.setAttribute('aria-labelledby', 'rd-confirm-title');
        confirmDialog.innerHTML =
            '<div class="rd-confirm-card">' +
            '<span class="rd-confirm-mark" aria-hidden="true">!</span>' +
            '<h2 class="rd-confirm-title" id="rd-confirm-title">Are you sure?</h2>' +
            '<p class="rd-confirm-message" data-confirm-message></p>' +
            '<div class="rd-confirm-actions">' +
            '<button type="button" class="rd-btn rd-btn-ghost" data-confirm-cancel>Go back</button>' +
            '<button type="button" class="rd-btn rd-btn-danger" data-confirm-ok>Yes, continue</button>' +
            '</div>' +
            '</div>';

        document.body.appendChild(confirmDialog);

        confirmDialog.querySelector('[data-confirm-cancel]').addEventListener('click', function () {
            confirmDialog.close('cancel');
        });

        confirmDialog.querySelector('[data-confirm-ok]').addEventListener('click', function () {
            confirmDialog.close('ok');
        });

        // A click on the backdrop, outside the card, counts as going back.
        confirmDialog.addEventListener('click', function (event) {
            if (event.target === confirmDialog) {
                confirmDialog.close('cancel');
            }
        });

        confirmDialog.addEventListener('close', function () {
            var form = pendingForm;
            pendingForm = null;

            if (form && confirmDialog.returnValue === 'ok') {
                form.dataset.confirmed = 'true';
                form.requestSubmit();
            }
        });

        return confirmDialog;
    }

    document.addEventListener('submit', function (event) {
        var form = event.target;
        var message = form.getAttribute('data-confirm');

        if (!message) {
            return;
        }

        // The user has already confirmed, so let this submit through once.
        if (form.dataset.confirmed === 'true') {
            delete form.dataset.confirmed;
            return;
        }

        event.preventDefault();

        var dialog = getConfirmDialog();
        pendingForm = form;
        dialog.querySelector('[data-confirm-message]').textContent = message;

        if (typeof dialog.showModal === 'function') {
            dialog.showModal();
            dialog.querySelector('[data-confirm-cancel]').focus();
        } else if (window.confirm(message)) {
            pendingForm = null;
            form.dataset.confirmed = 'true';
            form.requestSubmit();
        } else {
            pendingForm = null;
        }
    });

    /* ---------- Wizards ---------- */

    function stepFields(step) {
        return toArray(step.querySelectorAll('input, select, textarea')).filter(function (field) {
            return field.type !== 'hidden' && !field.disabled
                && !field.closest('[data-role-section][hidden]');
        });
    }
    function firstInvalid(step) {
        return stepFields(step).find(function (field) { return !field.checkValidity(); }) || null;
    }
    function reportStep(step) {
        var field = firstInvalid(step);
        if (field) { field.reportValidity(); field.focus(); return false; }
        return true;
    }
    function readValue(field) {
        if (field.tagName === 'SELECT') {
            var option = field.options[field.selectedIndex];
            return option ? option.text : '';
        }
        if (field.type === 'file') {
            if (field.files && field.files.length > 0) { return field.files[0].name; }
            return field.getAttribute('data-empty-text') || 'Not set';
        }
        if (field.type === 'datetime-local') {
            return field.value ? field.value.replace('T', ' ') : '';
        }
        return field.value;
    }
    function buildReview(form) {
        var list = form.querySelector('[data-review]');
        if (!list) { return; }
        list.innerHTML = '';
        toArray(form.querySelectorAll('[data-label]')).forEach(function (field) {
            if (field.closest('[data-review]') || field.closest('[data-role-section][hidden]')) { return; }
            if (field.type === 'radio' || field.type === 'checkbox') { return; }
            var term = document.createElement('dt');
            term.textContent = field.getAttribute('data-label');
            var detail = document.createElement('dd');
            detail.textContent = readValue(field) || 'Not set';
            list.appendChild(term); list.appendChild(detail);
        });
    }
    function initWizard(form) {
        var steps = toArray(form.querySelectorAll('[data-step]'));
        if (steps.length === 0) { return; }
        var labels = toArray(form.querySelectorAll('[data-step-label]'));
        var backButton = form.querySelector('[data-prev]');
        var nextButton = form.querySelector('[data-next]');
        var submitButton = form.querySelector('[data-submit]');
        var last = steps.length - 1;
        var current = 0;
        var errorStep = steps.findIndex(function (step) {
            return step.querySelector('.input-validation-error') !== null;
        });
        if (errorStep > -1) { current = errorStep; }

        function show(scrollIntoView) {
            steps.forEach(function (step, index) { step.hidden = index !== current; });
            labels.forEach(function (label, index) {
                label.classList.toggle('is-active', index === current);
                label.classList.toggle('is-done', index < current);
            });
            if (backButton) { backButton.hidden = current === 0; }
            if (nextButton) { nextButton.hidden = current === last; }
            if (submitButton) { submitButton.hidden = current !== last; }
            if (current === last) { buildReview(form); }
            if (scrollIntoView && form.scrollIntoView) {
                form.scrollIntoView({ behavior: prefersReducedMotion ? 'auto' : 'smooth', block: 'start' });
            }
        }

        if (nextButton) {
            nextButton.addEventListener('click', function () {
                if (reportStep(steps[current])) {
                    current = Math.min(current + 1, last);
                    show(true);
                }
            });
        }
        if (backButton) {
            backButton.addEventListener('click', function () {
                current = Math.max(current - 1, 0);
                show(true);
            });
        }
        form.addEventListener('submit', function (event) {
            var invalidStep = steps.findIndex(function (step) { return firstInvalid(step) !== null; });
            if (invalidStep > -1) {
                event.preventDefault();
                current = invalidStep;
                show(true);
                reportStep(steps[current]);
            }
        });
        show(false);
    }
    function initRoleSections(form) {
        var roleInputs = toArray(form.querySelectorAll('input[name="Role"]'));
        if (roleInputs.length === 0) { return; }
        var sections = toArray(form.querySelectorAll('[data-role-section]'));
        function applyRole() {
            var selected = form.querySelector('input[name="Role"]:checked');
            var role = selected ? selected.value : '';
            sections.forEach(function (section) {
                var active = section.getAttribute('data-role-section') === role;
                section.hidden = !active;
                toArray(section.querySelectorAll('[data-role-required]')).forEach(function (field) {
                    field.required = active;
                });
            });
        }
        roleInputs.forEach(function (input) { input.addEventListener('change', applyRole); });
        applyRole();
    }

    /* ---------- Pager ---------- */

    function initPager(root) {
        var grid = root.querySelector('[data-pager]');
        var items = toArray(root.querySelectorAll('[data-pager-item]'));
        var filter = root.querySelector('[data-filter]');
        var status = root.querySelector('[data-pager-status]');
        var noResults = root.querySelector('[data-no-results]');
        var controls = root.querySelector('[data-pager-controls]');
        var prevButton = root.querySelector('[data-prev-page]');
        var nextButton = root.querySelector('[data-next-page]');
        var label = root.querySelector('[data-page-label]');
        var pageSize = parseInt(grid.getAttribute('data-page-size'), 10) || 6;
        var page = 0;

        function render() {
            var query = filter ? filter.value.trim().toLowerCase() : '';
            var matches = items.filter(function (item) {
                return !query || item.getAttribute('data-search').indexOf(query) !== -1;
            });
            var pages = Math.max(1, Math.ceil(matches.length / pageSize));
            page = Math.min(page, pages - 1);
            items.forEach(function (item) { item.hidden = true; });
            matches.slice(page * pageSize, (page + 1) * pageSize).forEach(function (item) {
                item.hidden = false;
            });
            if (noResults) { noResults.hidden = matches.length > 0; }
            if (controls) { controls.hidden = pages <= 1; }
            if (prevButton) { prevButton.disabled = page === 0; }
            if (nextButton) { nextButton.disabled = page >= pages - 1; }
            if (label) { label.textContent = 'Page ' + (page + 1) + ' of ' + pages; }
            if (status) {
                status.textContent = matches.length + (matches.length === 1 ? ' event' : ' events');
            }
        }

        if (prevButton) {
            prevButton.addEventListener('click', function () {
                page = Math.max(0, page - 1);
                render();
                root.scrollIntoView({ behavior: prefersReducedMotion ? 'auto' : 'smooth', block: 'start' });
            });
        }
        if (nextButton) {
            nextButton.addEventListener('click', function () {
                page += 1;
                render();
                root.scrollIntoView({ behavior: prefersReducedMotion ? 'auto' : 'smooth', block: 'start' });
            });
        }
        if (filter) {
            filter.addEventListener('input', function () { page = 0; render(); });
        }
        render();
    }

    /* ---------- Header / scroll / back-to-top ---------- */

    function initHeader() {
        var header = document.querySelector('[data-header]');
        if (!header) { return; }
        function update() { header.classList.toggle('is-scrolled', window.scrollY > 24); }
        window.addEventListener('scroll', update, { passive: true });
        update();
    }
    function initToTop() {
        var button = document.querySelector('[data-to-top]');
        if (!button) { return; }
        window.addEventListener('scroll', function () {
            button.classList.toggle('is-visible', window.scrollY > 600);
        }, { passive: true });
        button.addEventListener('click', function () {
            window.scrollTo({ top: 0, behavior: prefersReducedMotion ? 'auto' : 'smooth' });
        });
    }

    /* ---------- Reveal / counters / parallax ---------- */

    function initReveal() {
        var items = toArray(document.querySelectorAll('[data-reveal]'));
        if (!('IntersectionObserver' in window) || prefersReducedMotion) {
            items.forEach(function (item) { item.classList.add('is-visible'); });
            return;
        }
        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.15 });
        items.forEach(function (item, index) {
            item.style.transitionDelay = (index % 4) * 80 + 'ms';
            observer.observe(item);
        });
    }
    function animateCount(element) {
        var target = parseInt(element.getAttribute('data-count'), 10) || 0;
        if (prefersReducedMotion) { element.textContent = target; return; }
        var start = null;
        var duration = 1400;
        function step(timestamp) {
            if (start === null) { start = timestamp; }
            var progress = Math.min((timestamp - start) / duration, 1);
            var eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = Math.round(target * eased);
            if (progress < 1) { window.requestAnimationFrame(step); }
        }
        window.requestAnimationFrame(step);
    }
    function initCounters() {
        var items = toArray(document.querySelectorAll('[data-count]'));
        if (items.length === 0) { return; }
        if (!('IntersectionObserver' in window)) {
            items.forEach(animateCount);
            return;
        }
        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    animateCount(entry.target);
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.5 });
        items.forEach(function (item) { item.textContent = '0'; observer.observe(item); });
    }
    function initParallax() {
        var hero = document.querySelector('[data-hero]');
        if (!hero || prefersReducedMotion || !canHover) { return; }
        var layers = toArray(hero.querySelectorAll('[data-parallax]'));
        hero.addEventListener('pointermove', function (event) {
            var rect = hero.getBoundingClientRect();
            var x = (event.clientX - rect.left) / rect.width - 0.5;
            var y = (event.clientY - rect.top) / rect.height - 0.5;
            layers.forEach(function (layer) {
                var depth = parseFloat(layer.getAttribute('data-parallax')) || 0;
                layer.style.transform = 'translate(' + (x * depth) + 'px, ' + (y * depth) + 'px)';
            });
        });
        hero.addEventListener('pointerleave', function () {
            layers.forEach(function (layer) { layer.style.transform = ''; });
        });
    }

    /* ---------- Role tabs ---------- */

    function initRoleTabs() {
        var root = document.querySelector('[data-role-tabs]');
        if (!root) { return; }
        var tabs = toArray(root.querySelectorAll('[data-role-tab]'));
        var panels = toArray(root.querySelectorAll('[data-role-panel]'));
        function select(name) {
            tabs.forEach(function (tab) {
                var active = tab.getAttribute('data-role-tab') === name;
                tab.classList.toggle('is-active', active);
                tab.setAttribute('aria-selected', active ? 'true' : 'false');
            });
            panels.forEach(function (panel) {
                var active = panel.getAttribute('data-role-panel') === name;
                panel.hidden = !active;
                if (active) {
                    panel.classList.remove('is-entering');
                    void panel.offsetWidth;
                    panel.classList.add('is-entering');
                }
            });
        }
        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                select(tab.getAttribute('data-role-tab'));
            });
        });
    }

    /* ---------- Toast system ---------- */

    var toastIcons = {
        success: '<svg viewBox="0 0 24 24"><path d="M20 6 9 17l-5-5"/></svg>',
        error: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 8v4M12 16h.01"/></svg>',
        warning: '<svg viewBox="0 0 24 24"><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4M12 17h.01"/></svg>',
        info: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 16v-4M12 8h.01"/></svg>'
    };

    function getToastContainer() {
        var el = document.querySelector('[data-toast-container]');
        if (!el) {
            el = document.createElement('div');
            el.className = 'rd-toasts';
            el.setAttribute('data-toast-container', '');
            el.setAttribute('aria-live', 'polite');
            el.setAttribute('aria-atomic', 'false');
            document.body.appendChild(el);
        }
        return el;
    }

    function toast(opts) {
        opts = opts || {};
        var type = opts.type || 'info';
        var title = opts.title || '';
        var message = opts.message || '';
        var duration = typeof opts.duration === 'number' ? opts.duration : 4500;
        var container = getToastContainer();

        var el = document.createElement('div');
        el.className = 'rd-toast rd-toast-' + type;
        el.setAttribute('role', type === 'error' ? 'alert' : 'status');

        el.innerHTML =
            '<div class="rd-toast-ico">' + (toastIcons[type] || toastIcons.info) + '</div>' +
            '<div class="rd-toast-body">' +
            (title ? '<strong class="rd-toast-title">' + title + '</strong>' : '') +
            (message ? '<span class="rd-toast-msg">' + message + '</span>' : '') +
            '</div>' +
            '<button class="rd-toast-close" type="button" aria-label="Dismiss notification">' +
            '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>' +
            '</button>';

        container.appendChild(el);
        requestAnimationFrame(function () {
            requestAnimationFrame(function () { el.classList.add('is-visible'); });
        });

        function dismiss() {
            el.classList.remove('is-visible');
            window.setTimeout(function () {
                if (el.parentNode) { el.parentNode.removeChild(el); }
            }, 350);
        }

        el.querySelector('.rd-toast-close').addEventListener('click', dismiss);
        if (duration > 0) { window.setTimeout(dismiss, duration); }
        return { dismiss: dismiss };
    }

    window.RaceDay = window.RaceDay || {};
    window.RaceDay.toast = toast;

    function initToastsFromData() {
        document.addEventListener('click', function (event) {
            var trigger = event.target.closest('[data-toast]');
            if (!trigger) { return; }
            event.preventDefault();
            toast({
                type: trigger.getAttribute('data-toast-type') || 'info',
                title: trigger.getAttribute('data-toast-title') || '',
                message: trigger.getAttribute('data-toast') || ''
            });
        });
    }

    /* ---------- Command palette ---------- */

    function initCommandPalette() {
        var root = document.querySelector('[data-cmdk]');
        if (!root) { return; }

        var input = root.querySelector('.rd-cmdk-input');
        var list = root.querySelector('[data-cmdk-list]');
        var empty = root.querySelector('[data-cmdk-empty]');
        var items = toArray(root.querySelectorAll('[data-cmdk-item]'));
        var selected = 0;

        if (list) { list.setAttribute('role', 'listbox'); }

        function open() {
            root.classList.add('is-open');
            root.setAttribute('aria-hidden', 'false');
            if (input) {
                input.value = '';
                render('');
                window.setTimeout(function () { input.focus(); }, 30);
            }
            if (document.body) { document.body.style.overflow = 'hidden'; }
        }

        function close() {
            root.classList.remove('is-open');
            root.setAttribute('aria-hidden', 'true');
            if (document.body) { document.body.style.overflow = ''; }
        }

        function isOpen() { return root.classList.contains('is-open'); }

        function render(query) {
            query = (query || '').trim().toLowerCase();
            var visible = 0;
            selected = 0;
            items.forEach(function (item) {
                var text = (item.getAttribute('data-cmdk-search') || item.textContent || '').toLowerCase();
                var match = !query || text.indexOf(query) !== -1;
                item.hidden = !match;
                item.setAttribute('aria-selected', 'false');
                if (match) {
                    visible++;
                    if (visible === 1) { item.setAttribute('aria-selected', 'true'); selected = 0; }
                }
            });
            if (empty) { empty.hidden = visible > 0; }

            var firstVisible = items.filter(function (i) { return !i.hidden; })[0];
            if (firstVisible) {
                selected = items.indexOf(firstVisible);
                firstVisible.setAttribute('aria-selected', 'true');
            }
        }

        function move(direction) {
            var visibleItems = items.filter(function (i) { return !i.hidden; });
            if (visibleItems.length === 0) { return; }
            var idx = visibleItems.findIndex(function (i) { return i.getAttribute('aria-selected') === 'true'; });
            if (idx < 0) { idx = 0; }
            var nextIdx = (idx + direction + visibleItems.length) % visibleItems.length;
            visibleItems.forEach(function (i) { i.setAttribute('aria-selected', 'false'); });
            visibleItems[nextIdx].setAttribute('aria-selected', 'true');
            visibleItems[nextIdx].scrollIntoView({ block: 'nearest' });
        }

        function activate() {
            var current = items.find(function (i) { return i.getAttribute('aria-selected') === 'true'; });
            if (!current) { return; }
            var link = current.querySelector('a') || current;
            if (link && link.href) {
                close();
                window.location.href = link.href;
            } else if (current.hasAttribute('data-cmdk-action')) {
                var action = current.getAttribute('data-cmdk-action');
                close();
                if (action === 'toast') {
                    toast({ type: 'info', title: 'Shortcuts', message: 'Press ⌘K / Ctrl+K to open the palette.' });
                }
            }
        }

        if (input) {
            input.addEventListener('input', function () { render(input.value); });
            input.addEventListener('keydown', function (event) {
                if (event.key === 'ArrowDown') { event.preventDefault(); move(1); }
                else if (event.key === 'ArrowUp') { event.preventDefault(); move(-1); }
                else if (event.key === 'Enter') { event.preventDefault(); activate(); }
                else if (event.key === 'Escape') { event.preventDefault(); close(); }
            });
        }

        items.forEach(function (item) {
            item.addEventListener('mouseenter', function () {
                items.forEach(function (i) { i.setAttribute('aria-selected', 'false'); });
                item.setAttribute('aria-selected', 'true');
            });
            item.addEventListener('click', function (event) {
                event.preventDefault();
                activate();
            });
        });

        root.addEventListener('click', function (event) {
            if (event.target === root) { close(); }
        });

        toArray(document.querySelectorAll('[data-cmdk-open]')).forEach(function (trigger) {
            trigger.addEventListener('click', function (event) {
                event.preventDefault();
                open();
            });
        });

        document.addEventListener('keydown', function (event) {
            var isPalette = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k';
            if (isPalette) {
                event.preventDefault();
                if (isOpen()) { close(); } else { open(); }
            }
            if (event.key === 'Escape' && isOpen()) {
                close();
            }
        });
    }

    /* ---------- Countdown timers ---------- */

    function initCountdowns() {
        var els = toArray(document.querySelectorAll('[data-countdown]'));
        if (els.length === 0) { return; }

        function pad(n) { return (n < 10 ? '0' : '') + n; }

        function tick(el) {
            var targetStr = el.getAttribute('data-target');
            var target = targetStr ? new Date(targetStr).getTime() : 0;
            var now = Date.now();
            var diff = Math.max(0, target - now);
            var days = Math.floor(diff / 86400000);
            var hours = Math.floor((diff % 86400000) / 3600000);
            var mins = Math.floor((diff % 3600000) / 60000);
            var secs = Math.floor((diff % 60000) / 1000);

            var cells = el.querySelectorAll('[data-countdown-cell]');
            if (cells.length === 4) {
                cells[0].querySelector('.rd-count-num').textContent = days;
                cells[1].querySelector('.rd-count-num').textContent = pad(hours);
                cells[2].querySelector('.rd-count-num').textContent = pad(mins);
                cells[3].querySelector('.rd-count-num').textContent = pad(secs);
            }

            if (diff <= 0) {
                el.classList.add('is-live');
                el.setAttribute('data-countdown-live', 'true');
            }
        }

        els.forEach(function (el) {
            tick(el);
            window.setInterval(function () { tick(el); }, 1000);
        });
    }

    /* ---------- Boot ---------- */

    document.addEventListener('DOMContentLoaded', function () {
        initAurora();
        initStarfield();
        initCursorGlow();
        initProgress();
        initPageTransitions();
        initHeader();
        initToTop();
        initReveal();
        initCounters();
        initParallax();
        initRoleTabs();
        initTilt();
        initMagnetic();
        initCommandPalette();
        initToastsFromData();
        initCountdowns();

        toArray(document.querySelectorAll('form[data-wizard]')).forEach(initRoleSections);
        toArray(document.querySelectorAll('form[data-wizard]')).forEach(initWizard);
        toArray(document.querySelectorAll('[data-pager-root]')).forEach(initPager);
    });
})();