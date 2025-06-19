// scheduleTable.js
class ScheduleTableHandler extends BaseTableHandler {
    constructor() {
        super({
            containerSelector: '#scheduleTableContainer',
            loadUrl: '/Admin/GetScheduleData',
            deleteUrl: '/Admin/DeleteSchedule'
        });
    }

    // Override to add additional filter parameters
    getAdditionalParams() {
        return {
            date: $('#dateFilter').val(),
            classId: $('#classFilter').val()
        };
    }

    // Override to add additional event listeners
    initializeEventListeners() {
        super.initializeEventListeners();

        // Add filter change handlers
        $('#dateFilter, #classFilter').on('change', () => {
            this.currentPage = 1;
            this.loadData();
        });
    }
}