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

    private val _hasMoreData = MutableStateFlow(true)
    val hasMoreData: StateFlow<Boolean> = _hasMoreData

    private var currentSkip = 0

    init {
        refreshRooms()
    }

    fun refreshRooms() {
        viewModelScope.launch {
            _isRefreshing.value = true
            try {
                val response = service.getRooms(envId = environmentId, skip = 0, count = pageSize)
                val newRooms = response.body()?.data ?: emptyList()
                _rooms.value = newRooms
                currentSkip = newRooms.size
                _hasMoreData.value = newRooms.size >= pageSize
            } catch (e: Exception) {
                Log.e("EnvironmentDetailViewModel", "Error refreshing rooms: ${e.localizedMessage}", e)
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
                val nextList = response.body()?.data ?: emptyList()
                if (nextList.isNotEmpty()) {
                    _rooms.value = _rooms.value + nextList
                    currentSkip += nextList.size
                    _hasMoreData.value = nextList.size >= pageSize
                } else {
                    _hasMoreData.value = false
                }
            } catch (e: Exception) {
                Log.e("EnvironmentDetailViewModel", "Error loading more rooms: ${e.localizedMessage}", e)
            } finally {
                _isLoadingMore.value = false
            }
        }
    }
}