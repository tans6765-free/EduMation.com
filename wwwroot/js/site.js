(() => {
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

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

    document.querySelectorAll('.lesson-video').forEach((video) => {
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
        let score = 0;
        const showQuestion = (index) => {
            questions.forEach((question, questionIndex) => question.classList.toggle('is-active', questionIndex === index));
            if (counter) counter.textContent = `Question ${index + 1} of ${questions.length}`;
        };

        questions.forEach((question, questionIndex) => {
            question.querySelectorAll('[data-answer]').forEach((answer) => {
                answer.addEventListener('click', () => {
                    if (question.dataset.answered) return;
                    question.dataset.answered = 'true';
                    const isCorrect = Number(answer.dataset.answer) === Number(question.dataset.correct);
                    if (isCorrect) score++;
                    question.querySelectorAll('[data-answer]').forEach((option) => {
                        option.disabled = true;
                        if (Number(option.dataset.answer) === Number(question.dataset.correct)) option.classList.add('is-correct');
                    });
                    if (!isCorrect) answer.classList.add('is-incorrect');
                    const feedback = question.querySelector('[data-feedback]');
                    feedback.hidden = false;
                    feedback.className = `answer-feedback ${isCorrect ? 'is-correct' : 'is-incorrect'}`;
                    feedback.innerHTML = `<strong>${isCorrect ? 'Correct' : "Let's understand this."}</strong><span>${question.dataset.explanation}</span>`;
                    fetch('/ai/attempt', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            lessonId: Number(practice.dataset.lessonId),
                            question: question.querySelector('h2')?.textContent || '',
                            answer: answer.textContent,
                            isCorrect,
                            score: isCorrect ? 1 : 0,
                            feedback: question.dataset.explanation
                        })
                    }).catch(() => undefined);
                    const next = question.querySelector('[data-next]');
                    next.hidden = false;
                    next.textContent = questionIndex === questions.length - 1 ? 'See my result' : 'Next question';
                });
            });
            question.querySelector('[data-next]')?.addEventListener('click', () => {
                if (current < questions.length - 1) {
                    current++;
                    showQuestion(current);
                } else {
                    questions.forEach((item) => item.classList.remove('is-active'));
                    result.hidden = false;
                    result.classList.add('is-visible');
                    result.querySelector('[data-score]').textContent = score;
                    result.querySelector('[data-result-message]').textContent = score === questions.length ? 'Excellent understanding. You are ready for the next lesson.' : 'Review the lesson once more, then try again.';
                }
            });
        });
    }
})();