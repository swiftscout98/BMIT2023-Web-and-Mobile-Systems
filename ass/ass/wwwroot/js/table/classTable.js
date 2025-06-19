// classTable.js
class ClassTableHandler extends BaseTableHandler {
    constructor() {
        super({
            containerSelector: '#classTableContainer',
            loadUrl: '/Admin/GetClassData',
            deleteUrl: '/Admin/DeleteClass'
        });
    }
}