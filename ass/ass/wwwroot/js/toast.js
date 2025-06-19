function showToast(message, type = 'info', duration = 3000) {
    const toastContainer = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    
    const toastContent = `
        <div class="toast-header">
            <span class="toast-title">${type.charAt(0).toUpperCase() + type.slice(1)}</span>
            <button class="toast-close">&times;</button>
        </div>
        <div class="toast-body">${message}</div>
        <div class="toast-progress"></div>
    `;
    
    toast.innerHTML = toastContent;
    toastContainer.appendChild(toast);
    
    setTimeout(() => {
        toast.classList.add('show');
    }, 10);
    
    const progressBar = toast.querySelector('.toast-progress');
    let width = 0;
    const interval = 10;
    const step = interval / duration * 100;
    
    const progress = setInterval(() => {
        width += step;
        progressBar.style.width = width + '%';
        if (width >= 100) {
            clearInterval(progress);
            toast.classList.remove('show');
            setTimeout(() => {
                toastContainer.removeChild(toast);
            }, 300);
        }
    }, interval);
    
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearInterval(progress);
        toast.classList.remove('show');
        setTimeout(() => {
            toastContainer.removeChild(toast);
        }, 300);
    });
}

