package org.yooud.airsense.env

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
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

    private val _hasMoreData = MutableStateFlow(true)
    val hasMoreData: StateFlow<Boolean> = _hasMoreData

    private var currentSkip = 0

    init {
        refreshEnvironments()
    }

    fun refreshEnvironments() {
        viewModelScope.launch {
            _isRefreshing.value = true
            try {
                val response = service.getEnvironments(skip = 0, count = pageSize)
                val newEnvironments = response.body()?.data ?: emptyList()
                _environments.value = newEnvironments
                currentSkip = newEnvironments.size
                _hasMoreData.value = newEnvironments.size >= pageSize
            } catch (e: Exception) {
                Log.e("EnvironmentViewModel", "Error refreshing environments: ${e.localizedMessage}", e)
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
                val nextList = response.body()?.data ?: emptyList()
                if (nextList.isNotEmpty()) {
                    _environments.value = _environments.value + nextList
                    currentSkip += nextList.size
                    _hasMoreData.value = nextList.size >= pageSize
                } else {
                    _hasMoreData.value = false
                }
            } catch (e: Exception) {
                Log.e("EnvironmentViewModel", "Error loading more environments: ${e.localizedMessage}", e)
            } finally {
                _isLoadingMore.value = false
            }
        }
    }
}
