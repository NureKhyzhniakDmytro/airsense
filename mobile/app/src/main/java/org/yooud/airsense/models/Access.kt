package org.yooud.airsense.models

fun isReadOnlyRole(role: String?): Boolean = role.equals("user", ignoreCase = true)

fun canManageRole(role: String?): Boolean = !isReadOnlyRole(role)

