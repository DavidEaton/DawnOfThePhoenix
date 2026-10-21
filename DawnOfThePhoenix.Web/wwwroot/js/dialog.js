const dialog = document.querySelector('#modal');
const content = document.querySelector('#modal-content');

export function showDialog(html) {
    replaceDialogContent(html);
    dialog.showModal();
}

export function replaceDialogContent(html) {
    content.innerHTML = html;
}

export function initializeDialog() {
    document.querySelector('.close').addEventListener('click', () => dialog.close());

    // A click outside the dialog's rectangle belongs to its backdrop.
    dialog.addEventListener('click', (event) => {
        if (event.target !== dialog) {
            return;
        }

        const bounds = dialog.getBoundingClientRect();
        if (
            event.clientX < bounds.left ||
            event.clientX > bounds.right ||
            event.clientY < bounds.top ||
            event.clientY > bounds.bottom
        ) {
            dialog.close();
        }
    });
}
