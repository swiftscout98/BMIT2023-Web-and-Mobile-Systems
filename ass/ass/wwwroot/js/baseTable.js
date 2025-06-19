// baseTable.js
class BaseTableHandler {
    constructor(options) {
        this.currentPage = 1;
        this.currentSort = '';
        this.currentDir = '';
        this.searchTimer = null;
        this.options = {
            containerSelector: '#studentTableContainer',
            searchInputSelector: '#searchInput',
            loadUrl: '/Admin/GetStudentData',
            deleteUrl: '/Admin/Delete',
            ...options
        };

        this.initializeEventListeners();
    }

    initializeEventListeners() {
        // Search input handling
        const searchInput = $(this.options.searchInputSelector);
        searchInput.off('input').on('input', () => {
            clearTimeout(this.searchTimer);
            this.searchTimer = setTimeout(() => {
                this.currentPage = 1;
                this.loadData();
            }, 500);
        });

        // Initialize data on page load
        this.loadData();
    }

    loadData(page = 1, sort = this.currentSort, dir = this.currentDir) {
        const searchTerm = $(this.options.searchInputSelector).val();

        $.ajax({
            url: this.options.loadUrl,
            data: {
                page: page,
                sort: sort,
                dir: dir,
                searchTerm: searchTerm
            },
            beforeSend: () => {
                $('#loader').show();
            },
            success: (result) => {
                $(this.options.containerSelector).html(result);
                this.currentPage = page;
                this.currentSort = sort;
                this.currentDir = dir;
            },
            complete: () => {
                $('#loader').hide();
            }
        });
    }

    sortData(column) {
        let dir = 'asc';
        if (this.currentSort === column) {
            dir = this.currentDir === 'asc' ? 'desc' : 'asc';
        }
        this.loadData(1, column, dir);
    }

    deleteItem(id, name) {
        if (confirm(`Are you sure you want to delete ${name}?`)) {
            $.ajax({
                url: this.options.deleteUrl,
                type: 'POST',
                data: { id: id },
                success: (response) => {
                    if (response.success) {
                        showToast(response.message, 'success');
                        this.loadData(this.currentPage);
                    } else {
                        showToast(response.message, 'error');
                    }
                },
                error: () => {
                    showToast('An error occurred while trying to delete.', 'error');
                }
            });
        }
    }
}