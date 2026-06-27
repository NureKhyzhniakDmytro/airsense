package org.yooud.airsense.env

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import org.yooud.airsense.models.Room
import org.yooud.airsense.network.ApiClient

class EnvironmentDetailViewModel(
    private val environmentId: Int,
    private val pageSize: Int = 20
) : ViewModel() {

    private val service = ApiClient.service

    private val _rooms = MutableStateFlow<List<Room>>(emptyList())
    val rooms: StateFlow<List<Room>> = _rooms

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing

    private val _isLoadingMore = MutableStateFlow(false)
    val isLoadingMore: StateFlow<Boolean> = _isLoadingMore

    private val _hasMoreData = MutableStateFlow(false)
    val hasMoreData: StateFlow<Boolean> = _hasMoreData

    private val _errorMessage = MutableStateFlow<String?>(null)
    val errorMessage: StateFlow<String?> = _errorMessage

    private var currentSkip = 0

    init {
        refreshRooms()
    }

    fun refreshRooms() {
        viewModelScope.launch {
            _isRefreshing.value = true
            try {
                val response = service.getRooms(envId = environmentId, skip = 0, count = pageSize)
                if (!response.isSuccessful) {
                    throw IllegalStateException("Unable to load rooms (${response.code()})")
                }
                val newRooms = response.body()?.data ?: emptyList()
                _rooms.value = newRooms
                currentSkip = newRooms.size
                _hasMoreData.value = newRooms.size >= pageSize
                _errorMessage.value = null
            } catch (e: Exception) {
                Log.e("EnvironmentDetailViewModel", "Error refreshing rooms: ${e.localizedMessage}", e)
                _hasMoreData.value = false
                _errorMessage.value = readableNetworkError(e, "Unable to load rooms")
            } finally {
                _isRefreshing.value = false
            }
        }
    }

    fun loadMoreRooms() {
        if (_isLoadingMore.value || _isRefreshing.value || !_hasMoreData.value) return

        viewModelScope.launch {
            _isLoadingMore.value = true
            try {
                val response = service.getRooms(envId = environmentId, skip = currentSkip, count = pageSize)
                if (!response.isSuccessful) {
                    throw IllegalStateException("Unable to load more rooms (${response.code()})")
                }
                val nextList = response.body()?.data ?: emptyList()
                if (nextList.isNotEmpty()) {
                    _rooms.value = _rooms.value + nextList
                    currentSkip += nextList.size
                    _hasMoreData.value = nextList.size >= pageSize
                } else {
                    _hasMoreData.value = false
                }
                _errorMessage.value = null
            } catch (e: Exception) {
                Log.e("EnvironmentDetailViewModel", "Error loading more rooms: ${e.localizedMessage}", e)
                _hasMoreData.value = false
                _errorMessage.value = readableNetworkError(e, "Unable to load more rooms")
            } finally {
                _isLoadingMore.value = false
            }
        }
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
