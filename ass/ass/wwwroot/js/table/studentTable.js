// studentTable.js
class StudentTableHandler extends BaseTableHandler {
    constructor() {
        super({
            containerSelector: '#studentTableContainer',
            loadUrl: '/Admin/GetStudentData',
            deleteUrl: '/Admin/Delete'
        });
    }

    // Add specific deleteStudent method
    deleteStudent(id, name) {
        // Call the base delete method
        this.deleteItem(id, name);
    }
}

// Make sure tableHandler is available globally
window.tableHandler = new StudentTableHandler();