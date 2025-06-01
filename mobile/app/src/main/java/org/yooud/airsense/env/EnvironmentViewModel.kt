package org.yooud.airsense.env

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.*
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

    private var currentSkip = pageSize

    init {
        refreshEnvironments()
    }

    fun refreshEnvironments() {
        viewModelScope.launch {
            _isRefreshing.value = true

            try {
                val response = service.getEnvironments(skip = 0, count = pageSize)
                _environments.value = response.body()!!.data
            } catch (e: Exception) {
                Log.e("EnvironmentViewModel", e.localizedMessage, e)
            } finally {
                _isRefreshing.value = false
            }
        }
    }

    fun loadMoreEnvironments() {
        if (_isLoadingMore.value || _isRefreshing.value) return

        viewModelScope.launch {
            _isLoadingMore.value = true

            try {
                val response = service.getEnvironments(skip = currentSkip, count = pageSize)
                val nextList = response.body()!!.data
                if (nextList.isNotEmpty()) {
                    _environments.value = _environments.value + nextList
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
