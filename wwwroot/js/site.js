$(function () {
    // Add show password toggle functionality
    $('.toggle-password').on('change', function () {
        const input = $(this).closest('.form-group').find('input');
        input.attr('type', input.attr('type') === 'password' ? 'text' : 'password');
    });

    // Add any other custom JavaScript here
});