package org.yooud.airsense.env

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import org.yooud.airsense.models.Environment
import org.yooud.airsense.network.ApiClient

class EnvironmentViewModel(
    private val pageSize: Int = 20
) : ViewModel() {

    private val service = ApiClient.service

    private val _environments = MutableStateFlow<List<Environment>>(emptyList())
    val environments: StateFlow<List<Environment>> = _environments

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing

    private val _isLoadingMore = MutableStateFlow(false)
    val isLoadingMore: StateFlow<Boolean> = _isLoadingMore

    private val _hasMoreData = MutableStateFlow(false)
    val hasMoreData: StateFlow<Boolean> = _hasMoreData

    private val _errorMessage = MutableStateFlow<String?>(null)
    val errorMessage: StateFlow<String?> = _errorMessage

    private val _roomCounts = MutableStateFlow<Map<Int, Int>>(emptyMap())
    val roomCounts: StateFlow<Map<Int, Int>> = _roomCounts

    private var currentSkip = 0

    init {
        refreshEnvironments()
    }

    fun refreshEnvironments() {
        viewModelScope.launch {
            _isRefreshing.value = true
            try {
                val response = service.getEnvironments(skip = 0, count = pageSize)
                if (!response.isSuccessful) {
                    throw IllegalStateException("Unable to load environments (${response.code()})")
                }
                val newEnvironments = response.body()?.data ?: emptyList()
                _environments.value = newEnvironments
                currentSkip = newEnvironments.size
                _hasMoreData.value = newEnvironments.size >= pageSize
                loadRoomCounts(newEnvironments)
                _errorMessage.value = null
            } catch (e: Exception) {
                Log.e("EnvironmentViewModel", "Error refreshing environments: ${e.localizedMessage}", e)
                _hasMoreData.value = false
                _errorMessage.value = readableNetworkError(e, "Unable to load environments")
            } finally {
                _isRefreshing.value = false
            }
        }
    }

    fun loadMoreEnvironments() {
        if (_isLoadingMore.value || _isRefreshing.value || !_hasMoreData.value) return

        viewModelScope.launch {
            _isLoadingMore.value = true
            try {
                val response = service.getEnvironments(skip = currentSkip, count = pageSize)
                if (!response.isSuccessful) {
                    throw IllegalStateException("Unable to load more environments (${response.code()})")
                }
                val nextList = response.body()?.data ?: emptyList()
                if (nextList.isNotEmpty()) {
                    _environments.value = _environments.value + nextList
                    currentSkip += nextList.size
                    _hasMoreData.value = nextList.size >= pageSize
                    loadRoomCounts(nextList)
                } else {
                    _hasMoreData.value = false
                }
                _errorMessage.value = null
            } catch (e: Exception) {
                Log.e("EnvironmentViewModel", "Error loading more environments: ${e.localizedMessage}", e)
                _hasMoreData.value = false
                _errorMessage.value = readableNetworkError(e, "Unable to load more environments")
            } finally {
                _isLoadingMore.value = false
            }
        }
    }

    private suspend fun loadRoomCounts(environments: List<Environment>) {
        val missing = environments.filterNot { _roomCounts.value.containsKey(it.id) }
        if (missing.isEmpty()) return

        val counts = coroutineScope {
            missing.map { environment ->
                async {
                    environment.id to runCatching {
                        service.getRooms(envId = environment.id, skip = 0, count = 1)
                            .body()
                            ?.pagination
                            ?.total ?: 0
                    }.getOrElse {
                        Log.w("EnvironmentViewModel", "Unable to load room count for env ${environment.id}: ${it.localizedMessage}")
                        0
                    }
                }
            }.awaitAll().toMap()
        }
        _roomCounts.value = _roomCounts.value + counts
    }

    private fun readableNetworkError(error: Exception, fallback: String): String {
        val message = error.localizedMessage ?: error.message
        return if (message?.contains("timeout", ignoreCase = true) == true) {
            "$fallback. Network request timed out."
        } else {
            message ?: fallback
        }
    }
}
