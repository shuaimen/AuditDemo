<template>
  <div class="row justify-content-center">
    <div class="col-12 col-sm-10 col-md-6 col-lg-4">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <h5 class="mb-3">登录</h5>
          <div class="alert alert-info py-2" v-if="hint">
            {{ hint }}
          </div>
          <div class="mb-3">
            <label class="form-label">账号</label>
            <input class="form-control" v-model="username" autocomplete="username" />
          </div>
          <div class="mb-3">
            <label class="form-label">密码</label>
            <input class="form-control" type="password" v-model="password" autocomplete="current-password" />
          </div>
          <div class="d-flex gap-2">
            <button class="btn btn-primary" :disabled="busy" @click="doLogin">
              {{ busy ? '登录中…' : '登录' }}
            </button>
            <button class="btn btn-outline-secondary" :disabled="busy" @click="fillDemo">填充Demo账号</button>
          </div>
          <div class="text-danger mt-3" v-if="err">{{ err }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { AuthApi } from '../api/endpoints'
import { apiErrorMessage } from '../api/http'

const router = useRouter()
const username = ref('')
const password = ref('')
const busy = ref(false)
const err = ref('')
const hint = ref('默认演示账号通常为：admin / admin（以你后端种子数据为准）')

function fillDemo() {
  username.value = 'admin'
  password.value = 'admin'
}

async function doLogin() {
  err.value = ''
  busy.value = true
  try {
    const data = await AuthApi.login(username.value.trim(), password.value)
    const token = data?.token || data?.Token || data?.access_token || (typeof data === 'string' ? data : '')
    if (!token) throw new Error('登录成功但未返回 token')
    localStorage.setItem('token', token)
    router.push('/audits')
  } catch (e) {
    err.value = apiErrorMessage(e)
  } finally {
    busy.value = false
  }
}
</script>
