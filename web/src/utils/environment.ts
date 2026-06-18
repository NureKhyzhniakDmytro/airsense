export const getRoleBadge = (role: string) => {
    switch (role) {
        case "admin":
            return "app-chip app-chip--danger";
        case "owner":
            return "app-chip app-chip--primary";
        default:
            return "app-chip app-chip--success";
    }
};
