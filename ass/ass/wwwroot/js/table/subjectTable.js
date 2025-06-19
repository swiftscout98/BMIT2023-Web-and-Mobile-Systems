class SubjectTableHandler extends BaseTableHandler {
    constructor() {
        super({
            containerSelector: '#subjectTableContainer',
            loadUrl: '/Admin/GetSubjectData',
            deleteUrl: '/Admin/DeleteSubject'
        });
    }
}