export const getRoleBadge = (role: string) => {
    switch (role) {
        case "admin":
            return "bg-red-100 text-red-700";
        case "owner":
            return "bg-purple-100 text-purple-700";
        default:
            return "bg-green-100 text-green-700";
    }
};