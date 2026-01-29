<template>
  <div>
    <NavBar />
    <div class="container-fluid py-3">
      <router-view />
    </div>
  </div>
</template>

<script setup>
import NavBar from './components/NavBar.vue'
</script>
