package org.yooud.airsense.models

data class PaginationResponse<T>(val data: List<T>, val pagination: Pagination)
