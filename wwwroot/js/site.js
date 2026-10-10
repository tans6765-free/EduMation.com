(() => {
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // Native <details> is click-controlled; open desktop menus as soon as the pointer enters.
    if (window.matchMedia('(hover: hover) and (pointer: fine)').matches) {
        document.querySelectorAll('.nav-dropdown').forEach((menu) => {
            let closeTimer;
            menu.addEventListener('pointerenter', () => {
                window.clearTimeout(closeTimer);
                menu.open = true;
            });
            menu.addEventListener('pointerleave', () => {
                closeTimer = window.setTimeout(() => {
                    if (!menu.matches(':focus-within')) menu.open = false;
                }, 140);
            });
        });
    }

    document.querySelectorAll('[data-reveal]').forEach((element) => {
        if (reducedMotion) {
            element.classList.add('is-visible');
            return;
        }
        const observer = new IntersectionObserver((entries, currentObserver) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    currentObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.12 });
        observer.observe(element);
    });

    document.querySelectorAll('.toggle-password').forEach((toggle) => {
        toggle.addEventListener('change', () => {
            const input = toggle.closest('.form-group')?.querySelector('input[type="password"], input[type="text"]');
            if (input) input.type = input.type === 'password' ? 'text' : 'password';
        });
    });

    const questionType = document.querySelector('[data-question-type]');
    if (questionType) {
        const options = document.querySelector('[data-options-group]');
        const indexGroup = document.querySelector('[data-correct-index]');
        const marks = document.querySelector('[data-mark-value]');
        const syncQuestionType = () => {
            const choiceType = questionType.value === 'Multiple Choice' || questionType.value === 'True/False';
            const marksByType = { 'Multiple Choice': 1, 'True/False': 1, 'Fill in the Blank': 1, 'Short Answer': 3, 'Conceptual Understanding': 3, 'Application/Problem Solving': 3, 'Calculation': 3, 'Explanation': 5, 'Broad Answer': 5 };
            if (indexGroup) indexGroup.hidden = !choiceType;
            if (options) options.hidden = !choiceType;
            if (marks) marks.textContent = `Marks: ${marksByType[questionType.value] || 0} (fixed by question type)`;
        };
        questionType.addEventListener('change', syncQuestionType);
        syncQuestionType();
    }

    const reminderForm = document.querySelector('.calendar-form');
    if (reminderForm) {
        const resetForm = () => {
            reminderForm.reset();
            reminderForm.querySelector('[data-reminder-id]').value = '';
            reminderForm.querySelector('[data-reminder-form-title]').textContent = 'Add a reminder';
            reminderForm.querySelector('[data-cancel-edit]').hidden = true;
        };
        document.querySelectorAll('[data-edit-reminder]').forEach((button) => button.addEventListener('click', () => {
            reminderForm.querySelector('[data-reminder-id]').value = button.dataset.id;
            reminderForm.querySelector('[name="Title"]').value = button.dataset.title || '';
            reminderForm.querySelector('[name="Category"]').value = button.dataset.category || 'Other';
            reminderForm.querySelector('[name="StartsAtLocal"]').value = button.dataset.start || '';
            reminderForm.querySelector('[name="Notes"]').value = button.dataset.notes || '';
            reminderForm.querySelector('[name="ReminderMinutes"]').value = button.dataset.lead || '1440';
            reminderForm.querySelector('[name="EmailNotification"][type="checkbox"]').checked = button.dataset.email === 'true';
            reminderForm.querySelector('[name="BrowserNotification"][type="checkbox"]').checked = button.dataset.browser === 'true';
            reminderForm.querySelector('[name="IsAllDay"][type="checkbox"]').checked = button.dataset.allDay === 'true';
            reminderForm.querySelector('[data-reminder-form-title]').textContent = 'Edit reminder';
            reminderForm.querySelector('[data-cancel-edit]').hidden = false;
            reminderForm.scrollIntoView({ behavior: reducedMotion ? 'auto' : 'smooth', block: 'start' });
        }));
        reminderForm.querySelector('[data-cancel-edit]')?.addEventListener('click', resetForm);
        document.querySelector('[data-browser-notification]')?.addEventListener('click', async (event) => {
            const button = event.currentTarget;
            if (!('Notification' in window)) { button.textContent = 'Browser alerts unavailable'; return; }
            const permission = await Notification.requestPermission();
            button.textContent = permission === 'granted' ? 'Browser alerts enabled' : 'Permission not enabled';
        });
    }

    if (document.querySelector('.notification-link')) {
        const notifyBrowser = async () => {
            try {
                const response = await fetch('/notifications/unread', { credentials: 'same-origin' });
                if (!response.ok) return;
                const notifications = await response.json();
                if (!('Notification' in window) || Notification.permission !== 'granted') return;
                const seen = new Set(JSON.parse(localStorage.getItem('edumation-notified') || '[]'));
                notifications.forEach((item) => {
                    if (seen.has(item.id)) return;
                    const alert = new Notification(item.title, { body: item.message, icon: '/images/logo.png', tag: `edumation-${item.id}` });
                    alert.onclick = () => { window.focus(); window.location.href = item.link; };
                    seen.add(item.id);
                });
                localStorage.setItem('edumation-notified', JSON.stringify([...seen].slice(-100)));
            } catch { /* notifications are an enhancement; the in-app inbox remains available */ }
        };
        notifyBrowser();
        window.setInterval(notifyBrowser, 60000);
    }

    document.querySelectorAll('.lesson-video').forEach((video) => {
        video.addEventListener('loadedmetadata', () => {
            const resumePosition = Number(video.dataset.resumePosition || 0);
            if (resumePosition > 0 && Number.isFinite(resumePosition) && resumePosition < video.duration - 5) video.currentTime = resumePosition;
        }, { once: true });
        let lastProgressSave = 0;
        video.addEventListener('timeupdate', () => {
            const now = Date.now();
            if (now - lastProgressSave < 5000 || !video.duration || !Number.isFinite(video.duration)) return;
            lastProgressSave = now;
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (!token || !video.dataset.progressUrl) return;
            fetch(video.dataset.progressUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ position: video.currentTime, duration: video.duration })
            }).then((response) => response.ok ? response.json() : null).then((progress) => {
                const button = document.querySelector('[data-lesson-complete="video"]');
                if (progress?.isCompleted && button) {
                    button.disabled = false;
                    button.textContent = 'Completed';
                }
                const label = document.querySelector('.progress-panel strong');
                const bar = document.querySelector('.progress-track span');
                if (progress && label && bar) {
                    label.textContent = `${progress.percentComplete}%`;
                    bar.style.width = `${progress.percentComplete}%`;
                }
            }).catch(() => undefined);
        });
        video.addEventListener('ended', () => {
            const moment = document.querySelector('[data-practice-moment]');
            if (moment) {
                moment.hidden = false;
                moment.classList.add('is-visible');
                moment.scrollIntoView({ behavior: reducedMotion ? 'auto' : 'smooth', block: 'center' });
            }
        });
    });

    const practice = document.querySelector('[data-practice]');
    if (practice) {
        const questions = [...practice.querySelectorAll('[data-question]')];
        const result = practice.querySelector('[data-practice-result]');
        const counter = document.querySelector('[data-practice-counter]');
        let current = 0;
        const showQuestion = (index) => {
            questions.forEach((question, questionIndex) => question.classList.toggle('is-active', questionIndex === index));
            if (counter) counter.textContent = `Question ${index + 1} of ${questions.length}`;
        };

        questions.forEach((question, questionIndex) => {
            const submitAnswer = (selectedOption, textAnswer, trigger) => {
                    if (question.dataset.answered) return;
                    question.dataset.answered = 'true';
                    const feedback = question.querySelector('[data-feedback]');
                    question.querySelectorAll('[data-answer]').forEach((option) => { option.disabled = true; });
                    const writtenInput = question.querySelector('[data-written-answer]');
                    const writtenButton = question.querySelector('[data-submit-written]');
                    if (writtenInput) writtenInput.disabled = true;
                    if (writtenButton) { writtenButton.disabled = true; writtenButton.textContent = 'Checking…'; }
                    trigger?.classList.add('is-pending');
                    feedback.hidden = false;
                    feedback.className = 'answer-feedback';
                    feedback.textContent = 'Checking your answer…';
                    const next = question.querySelector('[data-next]');
                    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                    fetch('/learn/practice/answer', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token || '' },
                        body: JSON.stringify({
                            lessonId: Number(practice.dataset.lessonId),
                            sessionId: practice.dataset.sessionId,
                            questionIndex,
                            selectedOption,
                            answer: textAnswer
                        })
                    }).then(async (response) => {
                        const payload = await response.json();
                        if (!response.ok) throw new Error(payload.message || 'Could not check this answer.');
                        const isCorrect = payload.isCorrect;
                        trigger?.classList.remove('is-pending');
                        question.querySelectorAll('[data-answer]').forEach((option) => {
                            option.disabled = true;
                            if (Number(option.dataset.answer) === Number(payload.correctOption)) option.classList.add('is-correct');
                        });
                        if (trigger && !isCorrect) trigger.classList.add('is-incorrect');
                        feedback.className = `answer-feedback ${isCorrect ? 'is-correct' : 'is-incorrect'}`;
                        feedback.replaceChildren();
                        const heading = document.createElement('strong');
                        heading.textContent = `You earned ${payload.score}/${payload.maximumScore} marks`;
                        const explanation = document.createElement('span');
                        explanation.textContent = payload.feedback;
                        feedback.append(heading, explanation);
                        if (payload.explanation && payload.feedback !== payload.explanation) {
                            const guide = document.createElement('span'); guide.textContent = `Review: ${payload.explanation}`; feedback.append(guide);
                        }
                        next.hidden = false;
                        next.textContent = questionIndex === questions.length - 1 ? 'See my result' : 'Next question';
                    }).catch((error) => {
                        question.dataset.answered = '';
                        question.querySelectorAll('[data-answer]').forEach((option) => { option.disabled = false; });
                        if (writtenInput) writtenInput.disabled = false;
                        if (writtenButton) { writtenButton.disabled = false; writtenButton.textContent = 'Check my answer'; }
                        trigger?.classList.remove('is-pending');
                        feedback.className = 'answer-feedback is-incorrect';
                        feedback.textContent = error.message;
                    });
            };
            question.querySelectorAll('[data-answer]').forEach((answer) => answer.addEventListener('click', () => submitAnswer(Number(answer.dataset.answer), null, answer)));
            question.querySelector('[data-submit-written]')?.addEventListener('click', () => {
                const textAnswer = question.querySelector('[data-written-answer]').value.trim();
                if (!textAnswer) { question.querySelector('[data-feedback]').hidden = false; question.querySelector('[data-feedback]').textContent = 'Write a short answer before checking it.'; return; }
                submitAnswer(-1, textAnswer, null);
            });
            question.querySelector('[data-next]')?.addEventListener('click', async () => {
                if (current < questions.length - 1) {
                    current++;
                    showQuestion(current);
                } else {
                    questions.forEach((item) => item.classList.remove('is-active'));
                    result.hidden = false;
                    result.classList.add('is-visible');
                    result.querySelector('[data-score]').textContent = '…';
                    result.querySelector('[data-result-message]').textContent = 'Calculating your result…';
                    try {
                        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                        const response = await fetch('/learn/practice/result', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token || '' },
                            body: JSON.stringify({ sessionId: practice.dataset.sessionId })
                        });
                        const serverResult = await response.json();
                        if (!response.ok) throw new Error(serverResult.message || 'Could not load your result.');
                        result.querySelector('[data-score]').textContent = serverResult.score;
                        result.querySelector('[data-maximum]').textContent = serverResult.maximumScore;
                        result.querySelector('[data-result-message]').textContent = serverResult.percent === 100
                            ? 'Excellent understanding. You are ready for the next lesson.'
                            : `${serverResult.percent}% complete. Review this lesson, then try again.`;
                    } catch (error) {
                        result.querySelector('[data-result-message]').textContent = error.message || 'Your result is unavailable. Please try again.';
                    }
                }
            });
        });
    }
})();
