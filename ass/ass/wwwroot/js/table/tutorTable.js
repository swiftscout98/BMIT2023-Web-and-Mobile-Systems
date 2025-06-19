class TutorTableHandler extends BaseTableHandler {
    constructor() {
        super({
            containerSelector: '#tutorTableContainer',
            loadUrl: '/Admin/GetTutorData',
            deleteUrl: '/Admin/DeleteTutor'
        });
    }
}
