package org.yooud.airsense.env

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import org.yooud.airsense.models.Room
import org.yooud.airsense.network.ApiClient

class EnvironmentDetailViewModel(
    private val environmentId: Int,
    private val pageSize: Int = 20
) : ViewModel() {

    private val service = ApiClient.service

    private val _rooms = MutableStateFlow<List<Room>>(emptyList())
    val rooms: StateFlow<List<Room>> = _rooms.asStateFlow()

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing.asStateFlow()

    private val _isLoadingMore = MutableStateFlow(false)
    val isLoadingMore: StateFlow<Boolean> = _isLoadingMore.asStateFlow()

    private var currentSkip = pageSize

    init {
        refreshRooms()
    }

    fun refreshRooms() {
        viewModelScope.launch {
            _isRefreshing.value = true

            try {
                Log.d("EnvironmentViewModel", service.toString())
                val response = service.getRooms(environmentId, skip = 0, count = pageSize)
                _rooms.value = response.body()!!.data
            } catch (e: Exception) {
                Log.e("EnvironmentViewModel", e.localizedMessage)
            } finally {
                _isRefreshing.value = false
            }
        }
    }

    fun loadMoreRooms() {
        if (_isLoadingMore.value || _isRefreshing.value) return

        viewModelScope.launch {
            _isLoadingMore.value = true

            try {
                val response = service.getRooms(environmentId, skip = currentSkip, count = pageSize)
                val nextList = response.body()!!.data
                if (nextList.isNotEmpty()) {
                    _rooms.value = _rooms.value + nextList
                    currentSkip += nextList.size
                }
            } catch (e: Exception) {
                Log.e("EnvironmentViewModel", e.localizedMessage)
            } finally {
                _isLoadingMore.value = false
            }
        }
    }
}
